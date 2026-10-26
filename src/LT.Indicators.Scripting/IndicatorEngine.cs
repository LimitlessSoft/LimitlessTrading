using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IEngines;
using LT.Indicators.Scripting.BuiltIns;
using LT.Indicators.Scripting.DtoMappings;
using LT.Indicators.Sdk;

namespace LT.Indicators.Scripting;

/// <summary>
/// <see cref="IIndicatorEngine"/> over the SDK: built-ins from <see cref="BuiltInIndicators"/>,
/// scripts compiled by <see cref="ScriptCompiler"/> into one collectible load context each.
/// Every indicator runs in a fresh instance inside a time budget; whatever it throws ends up in
/// its result. Registered as a singleton.
/// </summary>
public class IndicatorEngine : IIndicatorEngine
{
	private readonly Lock _lock = new();
	private readonly Dictionary<string, Func<Indicator>> _builtIns = [];
	private readonly List<IndicatorDefinitionDto> _builtInDefinitions = [];
	private readonly Dictionary<string, LoadedScript> _scripts = [];

	private sealed record LoadedScript(
		string Hash,
		string FileName,
		ScriptLoadContext? Context,
		Type? Type,
		IndicatorDefinitionDto Definition
	);

	public IndicatorEngine()
	{
		foreach (var (key, create) in BuiltInIndicators.All)
		{
			var id = IndicatorHelpers.GetBuiltInId(key);
			_builtIns[id] = create;
			var output = IndicatorHost.Describe(create, IndicatorConstants.ComputeBudget);
			_builtInDefinitions.Add(ToDefinition(id, IndicatorSource.BuiltIn, output));
		}
	}

	public IReadOnlyList<IndicatorDefinitionDto> GetBuiltInDefinitions() =>
		[.. _builtInDefinitions.Select(Copy)];

	public string GetSdkAssemblyPath() => typeof(Indicator).Assembly.Location;

	public IndicatorDefinitionDto CompileScript(
		string definitionId,
		string fileName,
		string filePath,
		string source,
		CancellationToken cancellationToken = default
	)
	{
		var hash = ScriptCompiler.GetHash(source);
		lock (_lock)
			if (_scripts.TryGetValue(definitionId, out var current) && current.Hash == hash)
				return Copy(current.Definition);

		var compiled = ScriptCompiler.Compile(fileName, source, cancellationToken);
		var definition = new IndicatorDefinitionDto
		{
			Id = definitionId,
			Name = Path.GetFileNameWithoutExtension(fileName),
			ShortName = Path.GetFileNameWithoutExtension(fileName),
			Source = IndicatorSource.Script,
			FilePath = filePath,
			Diagnostics = compiled.Diagnostics,
		};
		ScriptLoadContext? context = null;
		Type? type = null;
		if (!compiled.Success)
			definition.Error = IndicatorHelpers.DescribeErrors(compiled.Diagnostics);
		else
		{
			context = new ScriptLoadContext($"script:{fileName}");
			try
			{
				using var assembly = new MemoryStream(compiled.Assembly!);
				using var pdb = new MemoryStream(compiled.Pdb!);
				type = context.LoadFromStream(assembly, pdb).GetType(compiled.TypeName!, true);
				var loaded = type!;
				var output = IndicatorHost.Describe(
					() => Create(loaded),
					IndicatorConstants.ComputeBudget
				);
				var described = ToDefinition(definitionId, IndicatorSource.Script, output);
				described.FilePath = filePath;
				described.Diagnostics = compiled.Diagnostics;
				definition = described;
			}
			catch (Exception ex)
			{
				// It must at least run on zero bars (that is how its inputs are learnt)
				definition.Error = $"Fails with no bars: {DescribeException(ex, fileName)}";
				type = null;
			}
		}

		lock (_lock)
		{
			if (_scripts.TryGetValue(definitionId, out var old))
				old.Context?.Unload();
			_scripts[definitionId] = new LoadedScript(hash, fileName, context, type, definition);
		}
		if (type == null)
			context?.Unload();
		return Copy(definition);
	}

	public void RemoveScript(string definitionId)
	{
		lock (_lock)
			if (_scripts.Remove(definitionId, out var old))
				old.Context?.Unload();
	}

	public IReadOnlyList<IndicatorResultDto> Compute(
		IReadOnlyList<BarDto> bars,
		bool isIntraday,
		IReadOnlyList<IndicatorRunDto> runs,
		TimeSpan budget
	)
	{
		var data = ToBarData(bars, isIntraday);
		var results = new List<IndicatorResultDto>(runs.Count);
		foreach (var run in runs)
			results.Add(Compute(data, run, budget));
		return results;
	}

	private IndicatorResultDto Compute(BarData bars, IndicatorRunDto run, TimeSpan budget)
	{
		var result = new IndicatorResultDto
		{
			DefinitionId = run.DefinitionId,
			BarCount = bars.Count,
		};
		Func<Indicator>? create;
		string? fileName = null;
		lock (_lock)
		{
			if (_scripts.TryGetValue(run.DefinitionId, out var script))
			{
				fileName = script.FileName;
				create = script.Type is { } type ? () => Create(type) : null;
				result.Error = script.Definition.Error;
			}
			else
				create = _builtIns.GetValueOrDefault(run.DefinitionId);
		}
		if (create == null)
		{
			result.Error ??= "This indicator is not loaded.";
			return result;
		}

		try
		{
			var output = IndicatorHost.Run(create, bars, run.Inputs, budget);
			result.Pane = IndicatorPlotDtoMapper.ToPane(output.Pane);
			result.ScaleMin = output.ScaleMin;
			result.ScaleMax = output.ScaleMax;
			result.Decimals = output.Decimals;
			var mapper = new IndicatorPlotDtoMapper();
			result.Plots = [.. output.Plots.Select(mapper.ToMapped)];
		}
		catch (Exception ex)
		{
			result.Error = DescribeException(ex, fileName);
			result.Plots = [];
		}
		return result;
	}

	private static Indicator Create(Type type)
	{
		try
		{
			return (Indicator)Activator.CreateInstance(type)!;
		}
		catch (TargetInvocationException ex) when (ex.InnerException != null)
		{
			ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
			throw;
		}
	}

	private static BarData ToBarData(IReadOnlyList<BarDto> bars, bool isIntraday)
	{
		var count = bars.Count;
		var time = new DateTimeOffset[count];
		double[] open = new double[count],
			high = new double[count],
			low = new double[count],
			close = new double[count],
			volume = new double[count];
		for (var i = 0; i < count; i++)
		{
			var bar = bars[i];
			time[i] = bar.Time;
			open[i] = bar.Open;
			high[i] = bar.High;
			low[i] = bar.Low;
			close[i] = bar.Close;
			volume[i] = (double)bar.Volume;
		}
		return new BarData(
			time,
			open,
			high,
			low,
			close,
			volume,
			isIntraday,
			TradeJournalHelpers.GetTradeDayTimeZone()
		);
	}

	private static IndicatorDefinitionDto ToDefinition(
		string id,
		IndicatorSource source,
		IndicatorOutput output
	)
	{
		var mapper = new IndicatorInputDtoMapper();
		return new IndicatorDefinitionDto
		{
			Id = id,
			Name = output.Name,
			ShortName = output.ShortName ?? output.Name,
			Source = source,
			Pane = IndicatorPlotDtoMapper.ToPane(output.Pane),
			Inputs = [.. output.Inputs.Select(mapper.ToMapped)],
		};
	}

	// "line 12: DivideByZeroException: …" — the script's own frame when it has one
	private static string DescribeException(Exception ex, string? fileName)
	{
		var message = ex switch
		{
			IndicatorTimeoutException => ex.Message,
			InsufficientExecutionStackException =>
				"Recursion too deep: stopped before the stack ran out.",
			_ => $"{ex.GetType().Name}: {ex.Message}",
		};
		if (fileName == null)
			return message;
		var line = new StackTrace(ex, true)
			.GetFrames()
			.FirstOrDefault(x => x.GetFileName() == fileName && x.GetFileLineNumber() > 0)
			?.GetFileLineNumber();
		return line is { } n ? $"line {n}: {message}" : message;
	}

	private static IndicatorDefinitionDto Copy(IndicatorDefinitionDto source) =>
		new()
		{
			Id = source.Id,
			Name = source.Name,
			ShortName = source.ShortName,
			Source = source.Source,
			Pane = source.Pane,
			Inputs =
			[
				.. source.Inputs.Select(x => new IndicatorInputDto
				{
					Name = x.Name,
					Type = x.Type,
					DefaultValue = x.DefaultValue,
					Min = x.Min,
					Max = x.Max,
					Step = x.Step,
				}),
			],
			FilePath = source.FilePath,
			IsCompiling = source.IsCompiling,
			Error = source.Error,
			Diagnostics = [.. source.Diagnostics],
		};
}
