using LSCore.Exceptions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.Indicators;
using LT.Domain.Managers;
using LT.Indicators.Scripting;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

public class IndicatorManagerTests
{
	private readonly InMemoryIndicatorScriptRepository _scripts = new();
	private readonly MessageLogManager _messages = new();
	private readonly IndicatorManager _manager;

	public IndicatorManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new IndicatorManager(new IndicatorEngine(), _scripts, _messages);
	}

	private static List<BarDto> Bars(int count) =>
		[
			.. Enumerable
				.Range(0, count)
				.Select(i => new BarDto
				{
					Time = new DateTimeOffset(2026, 10, 26, 14, 30, 0, TimeSpan.Zero).AddMinutes(
						5 * i
					),
					Open = 100 + Math.Sin(i),
					High = 102 + Math.Sin(i),
					Low = 98 + Math.Sin(i),
					Close = 100 + Math.Cos(i),
					Volume = 1000 + i,
				}),
		];

	private static string BuiltIn(string key) => IndicatorHelpers.GetBuiltInId(key);

	private IReadOnlyList<IndicatorResultDto> Compute(params IndicatorInstanceDto[] instances) =>
		_manager.Compute(
			new ComputeIndicatorsRequest
			{
				Bars = Bars(60),
				Indicators = instances,
				IsIntraday = true,
			}
		);

	private static async Task WaitUntil(Func<bool> condition)
	{
		var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(30);
		while (!condition())
		{
			if (DateTime.UtcNow > timeout)
				throw new TimeoutException("Condition not met in time.");
			await Task.Delay(20);
		}
	}

	[Fact]
	public void The_catalog_lists_the_built_ins_with_their_inputs()
	{
		var definitions = _manager.GetDefinitions();

		Assert.Equal(
			["sma", "ema", "vwap", "bb", "rsi", "macd", "atr", "volume", "volume-ma", "rvol"],
			definitions.Select(x => x.Id[IndicatorConstants.BuiltInIdPrefix.Length..])
		);
		var rsi = _manager.GetDefinition(BuiltIn("rsi"))!;
		Assert.Equal((IndicatorPane.Separate, "RSI"), (rsi.Pane, rsi.ShortName));
		var length = Assert.Single(rsi.Inputs);
		Assert.Equal(
			("Length", IndicatorInputType.Int, 14d),
			(length.Name, length.Type, length.DefaultValue)
		);
		Assert.Equal(IndicatorPane.Volume, _manager.GetDefinition(BuiltIn("volume-ma"))!.Pane);
		Assert.Equal(
			["Length", "Deviations"],
			_manager.GetDefinition(BuiltIn("bb"))!.Inputs.Select(x => x.Name)
		);
	}

	[Fact]
	public void A_new_instance_starts_with_the_defaults()
	{
		var instance = _manager.CreateInstance(BuiltIn("macd"));

		Assert.Equal(BuiltIn("macd"), instance.DefinitionId);
		Assert.Equal(
			new Dictionary<string, double>
			{
				["Fast"] = 12,
				["Slow"] = 26,
				["Signal"] = 9,
			},
			instance.Inputs
		);
		Assert.True(instance.IsVisible);
		Assert.NotEqual(_manager.CreateInstance(BuiltIn("macd")).Id, instance.Id);
	}

	[Fact]
	public void An_unknown_indicator_cant_be_added()
	{
		var ex = Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.CreateInstance("builtin:nope")
		);
		Assert.Contains("no longer available", ex.Message);
	}

	[Theory]
	[InlineData("Length", 0, "at least 1")]
	[InlineData("Length", 1001, "at most 1000")]
	[InlineData("Length", 2.5, "whole number")]
	[InlineData("Length", double.NaN, "must be a number")]
	[InlineData("Deviations", 0.05, "at least 0.1")]
	[InlineData("Colour", 1, "not an input")]
	public void Inputs_are_validated_against_the_definition(
		string name,
		double value,
		string expected
	)
	{
		var definition = _manager.GetDefinition(BuiltIn("bb"))!;
		var instance = _manager.CreateInstance(definition.Id);

		var ex = Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.UpdateInputs(
				new UpdateIndicatorInputsRequest
				{
					Definition = definition,
					Instance = instance,
					Inputs = new() { [name] = value },
				}
			)
		);
		Assert.Contains(expected, ex.Message);
	}

	[Fact]
	public void Bool_inputs_must_be_on_or_off()
	{
		var definition = new IndicatorDefinitionDto
		{
			Id = "script:x.cs",
			Inputs = [new IndicatorInputDto { Name = "Show", Type = IndicatorInputType.Bool }],
		};

		var ex = Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.UpdateInputs(
				new UpdateIndicatorInputsRequest
				{
					Definition = definition,
					Instance = new IndicatorInstanceDto { DefinitionId = definition.Id },
					Inputs = new() { ["Show"] = 2 },
				}
			)
		);
		Assert.Contains("on or off", ex.Message);
	}

	[Fact]
	public void Valid_inputs_update_the_instance_and_its_label()
	{
		var definition = _manager.GetDefinition(BuiltIn("bb"))!;
		var instance = _manager.CreateInstance(definition.Id);

		var updated = _manager.UpdateInputs(
			new UpdateIndicatorInputsRequest
			{
				Definition = definition,
				Instance = instance,
				Inputs = new() { ["Length"] = 10, ["Deviations"] = 2.5 },
			}
		);

		Assert.Equal(instance.Id, updated.Id);
		Assert.Equal(10, updated.Inputs["Length"]);
		Assert.Equal("BB 10 2.5", Compute(updated).Single().Label);
	}

	[Fact]
	public void Compute_returns_one_result_per_instance_in_order()
	{
		var sma = _manager.CreateInstance(BuiltIn("sma"));
		var rsi = _manager.CreateInstance(BuiltIn("rsi"));
		var hidden = _manager.CreateInstance(BuiltIn("macd"));
		hidden.IsVisible = false;

		var results = Compute(sma, rsi, hidden);

		Assert.Equal([sma.Id, rsi.Id, hidden.Id], results.Select(x => x.InstanceId));
		Assert.Equal(["SMA 20", "RSI 14", "MACD 12 26 9"], results.Select(x => x.Label));
		Assert.All(results, x => Assert.Null(x.Error));
		Assert.Equal(60, Assert.Single(results[0].Plots).Values.Length);
		Assert.Equal((0d, 100d), (results[1].ScaleMin!.Value, results[1].ScaleMax!.Value));
		// Hidden: listed in the legend, not computed
		Assert.False(results[2].IsVisible);
		Assert.Empty(results[2].Plots);
		Assert.Equal(IndicatorPane.Separate, results[2].Pane);
	}

	[Fact]
	public void A_saved_input_that_is_no_longer_valid_falls_back_to_its_default()
	{
		var sma = _manager.CreateInstance(BuiltIn("sma"));
		sma.Inputs["Length"] = 5000;
		sma.Inputs["Gone"] = 3;

		var result = Compute(sma).Single();

		Assert.Equal("SMA 20", result.Label);
		Assert.Equal(19, result.Plots[0].Values.TakeWhile(double.IsNaN).Count());
	}

	[Fact]
	public void An_instance_of_a_missing_script_reports_it()
	{
		var result = Compute(new IndicatorInstanceDto { DefinitionId = "script:gone.cs" }).Single();

		Assert.Equal("gone", result.Label);
		Assert.Contains("gone", result.Error);
		Assert.Empty(result.Plots);
	}

	[Fact]
	public void A_chart_has_a_limit_of_indicators()
	{
		var many = Enumerable
			.Range(0, IndicatorConstants.MaxIndicatorsPerChart + 1)
			.Select(_ => _manager.CreateInstance(BuiltIn("sma")))
			.ToArray();

		var ex = Assert.Throws<LSCoreBadRequestException>(() => Compute(many));
		Assert.Contains($"at most {IndicatorConstants.MaxIndicatorsPerChart}", ex.Message);
	}

	[Fact]
	public async Task Scripts_are_compiled_on_start_and_again_when_their_file_changes()
	{
		_scripts.Seed("fast.cs", "Plot(\"Fast\", Ta.Ema(Close, Input(\"Length\", 5, 1, 50)));");
		var changed = new List<string>();
		_manager.DefinitionsChanged += ids =>
		{
			lock (changed)
				changed.AddRange(ids);
		};
		using var stop = new CancellationTokenSource();
		var run = _manager.RunAsync(stop.Token);

		var id = IndicatorHelpers.GetScriptId("fast.cs");
		await WaitUntil(() => _manager.GetDefinition(id) is { IsCompiling: false });
		Assert.True(_scripts.IsWatching);
		Assert.Contains(
			_scripts.Workspace!,
			x => x.RelativePath == IndicatorConstants.ProjectFileName
		);
		var definition = _manager.GetDefinition(id)!;
		Assert.Null(definition.Error);
		Assert.Equal(("fast", IndicatorSource.Script), (definition.Name, definition.Source));
		Assert.Equal("Length", Assert.Single(definition.Inputs).Name);
		var instance = _manager.CreateInstance(id);
		Assert.Equal("Fast", Assert.Single(Compute(instance).Single().Plots).Name);

		// A broken save: the error is on the definition and in the message log
		_scripts.Write("fast.cs", "Plot(\"Fast\", Close");
		await WaitUntil(() =>
			_manager.GetDefinition(id) is { IsCompiling: false, Error: not null }
		);
		Assert.Contains("line 1", _manager.GetDefinition(id)!.Error);
		Assert.Contains(_messages.GetAll(), x => x.Text.StartsWith("fast.cs: line 1"));
		Assert.NotNull(Compute(instance).Single().Error);

		// Fixed again
		_scripts.Write("fast.cs", "Plot(\"Slow\", Ta.Sma(Close, 3));");
		await WaitUntil(() => _manager.GetDefinition(id) is { IsCompiling: false, Error: null });
		Assert.Equal("Slow", Assert.Single(Compute(instance).Single().Plots).Name);

		// Deleted
		_scripts.Delete("fast.cs");
		await WaitUntil(() => _manager.GetDefinition(id) == null);
		lock (changed)
			Assert.Contains(id, changed);

		stop.Cancel();
		await run;
	}
}
