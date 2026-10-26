using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Indicators.Scripting;

namespace LT.Tests.Indicators;

/// <summary>Script files through the real compiler and engine (Roslyn, collectible load contexts).</summary>
public class IndicatorScriptTests
{
	private readonly IndicatorEngine _engine = new();

	private static List<BarDto> Bars(int count) =>
		[
			.. Enumerable
				.Range(0, count)
				.Select(i => new BarDto
				{
					Time = new DateTimeOffset(2026, 10, 26, 14, 30, 0, TimeSpan.Zero).AddMinutes(i),
					Open = 100 + i,
					High = 101 + i,
					Low = 99 + i,
					Close = 100.5 + i,
					Volume = 1000,
				}),
		];

	private IndicatorDefinitionDto Compile(string source, string fileName = "test.cs") =>
		_engine.CompileScript(
			IndicatorHelpers.GetScriptId(fileName),
			fileName,
			"/scripts/" + fileName,
			source
		);

	private IndicatorResultDto Run(
		string fileName,
		int bars = 30,
		Dictionary<string, double>? inputs = null,
		TimeSpan? budget = null
	) =>
		_engine
			.Compute(
				Bars(bars),
				true,
				[
					new IndicatorRunDto
					{
						DefinitionId = IndicatorHelpers.GetScriptId(fileName),
						Inputs = inputs ?? [],
					},
				],
				budget ?? TimeSpan.FromSeconds(5)
			)
			.Single();

	[Fact]
	public void Top_level_script_declares_its_inputs_and_plots_with_the_given_values()
	{
		var definition = Compile(
			"""
			Name = "My EMA";
			var length = Input("Length", 9, 1, 100);
			Plot("EMA", Ta.Ema(Close, length), Colors.Teal);
			"""
		);

		Assert.Null(definition.Error);
		Assert.Equal("My EMA", definition.Name);
		var input = Assert.Single(definition.Inputs);
		Assert.Equal(
			("Length", IndicatorInputType.Int, 9d, 1d, 100d),
			(input.Name, input.Type, input.DefaultValue, input.Min, input.Max)
		);

		var result = Run("test.cs", inputs: new() { ["Length"] = 3 });
		Assert.Null(result.Error);
		var plot = Assert.Single(result.Plots);
		Assert.Equal(30, plot.Values.Length);
		Assert.True(double.IsNaN(plot.Values[1]));
		// SMA seed of closes 100.5, 101.5, 102.5
		Assert.Equal(101.5, plot.Values[2], 9);
	}

	[Fact]
	public void A_single_expression_is_drawn_as_one_line_named_after_the_file()
	{
		var definition = Compile("Ta.Sma(Close, 2)", "sma-two.cs");

		Assert.Null(definition.Error);
		Assert.Equal("sma-two", definition.Name);
		var plot = Assert.Single(Run("sma-two.cs").Plots);
		Assert.Equal("sma-two", plot.Name);
		Assert.Equal(101, plot.Values[1], 9);
	}

	[Fact]
	public void A_script_can_choose_its_own_pane()
	{
		Compile(
			"""
			Pane = PaneKind.Separate;
			ScaleMin = 0;
			ScaleMax = 100;
			Plot("RSI", Ta.Rsi(Close, 14));
			Level("High", 70);
			"""
		);

		var result = Run("test.cs");
		Assert.Null(result.Error);
		Assert.Equal(
			(IndicatorPane.Separate, 0d, 100d),
			(result.Pane, result.ScaleMin, result.ScaleMax)
		);
		Assert.Equal(
			[IndicatorPlotKind.Line, IndicatorPlotKind.Level],
			result.Plots.Select(x => x.Kind)
		);
	}

	[Fact]
	public void A_class_deriving_from_Indicator_is_compiled_as_it_is()
	{
		var definition = Compile(
			"""
			public class Spread : Indicator
			{
				public Spread() => Name = "Spread";

				protected override void Compute() =>
					Histogram("Range", High - Low, Colors.Blue);
			}
			"""
		);

		Assert.Null(definition.Error);
		Assert.Equal("Spread", definition.Name);
		Assert.All(Assert.Single(Run("test.cs").Plots).Values, x => Assert.Equal(2, x, 9));
	}

	[Fact]
	public void Helper_functions_and_types_in_a_script_work()
	{
		Compile(
			"""
			Series Double(Series s) => s * 2;
			var helper = new Helper();
			Plot("Twice", Double(Close) + helper.Offset());

			class Helper
			{
				public double Offset() => Close.Length > 0 ? 0 : 0;
			}
			"""
		);

		var result = Run("test.cs");
		Assert.Null(result.Error);
		Assert.Equal(201, result.Plots[0].Values[0], 9);
	}

	[Fact]
	public void Compile_errors_carry_the_line_of_the_file()
	{
		var definition = Compile(
			"""
			var a = Ta.Sma(Close, 3);
			var b = a +* 2;
			Plot("A", a);
			"""
		);

		Assert.NotNull(definition.Error);
		Assert.Contains(definition.Diagnostics, x => x.IsError && x.Line == 2);
		Assert.StartsWith("line 2:", definition.Error);
	}

	[Fact]
	public void Unknown_names_are_reported_on_their_line()
	{
		var definition = Compile(
			"""
			// comment
			Plot("X", Ta.NoSuchFunction(Close));
			"""
		);

		var error = Assert.Single(definition.Diagnostics, x => x.IsError);
		Assert.Equal(2, error.Line);
		Assert.Contains("NoSuchFunction", error.Message);
	}

	[Theory]
	[InlineData("var text = System.IO.File.ReadAllText(\"/etc/passwd\");", "System.IO")]
	[InlineData("using System.IO;\nvar exists = File.Exists(\"x\");", "System.IO")]
	[InlineData("var http = new System.Net.Http.HttpClient();", "System.Net")]
	[InlineData("System.Diagnostics.Process.Start(\"sh\");", "System.Diagnostics")]
	[InlineData("var name = typeof(int).Assembly.FullName;", "System.Type")]
	[InlineData("var t = Close.GetType();", "System.Type")]
	[InlineData("System.Environment.Exit(1);", "System.Environment")]
	[InlineData("var v = Environment.GetEnvironmentVariable(\"HOME\");", "System.Environment")]
	[InlineData("new System.Threading.Thread(() => { }).Start();", "System.Threading")]
	[InlineData("System.Threading.Tasks.Task.Run(() => { });", "System.Threading")]
	[InlineData("var o = Activator.CreateInstance<object>();", "System.Activator")]
	[InlineData("System.Runtime.InteropServices.Marshal.AllocHGlobal(8);", "System.Runtime")]
	[InlineData("var o = new object();\nlock (o) { }", "lock")]
	[InlineData("Func<int> f = () => 1;\nvar m = f.Method;", "System.Reflection")]
	public void Banned_apis_are_rejected_at_compile_time(string script, string expected)
	{
		var definition = Compile(script + "\nPlot(\"C\", Close);");

		Assert.NotNull(definition.Error);
		Assert.Contains(definition.Diagnostics, x => x.IsError && x.Message.Contains(expected));
		var result = Run("test.cs");
		Assert.NotNull(result.Error);
		Assert.Empty(result.Plots);
	}

	[Fact]
	public void Native_code_is_rejected()
	{
		var definition = Compile(
			"""
			public class Native : Indicator
			{
				[System.Runtime.InteropServices.DllImport("libc")]
				private static extern int getpid();

				protected override void Compute() => Plot("C", Close);
			}
			"""
		);

		Assert.NotNull(definition.Error);
		Assert.Contains(definition.Diagnostics, x => x.Message.Contains("extern"));
	}

	[Fact]
	public void Unsafe_code_is_rejected()
	{
		var definition = Compile(
			"""
			unsafe { int x = 1; int* p = &x; }
			Plot("C", Close);
			"""
		);

		Assert.NotNull(definition.Error);
	}

	[Fact]
	public void A_runtime_exception_is_the_indicators_error_with_its_line_and_others_still_compute()
	{
		Compile(
			"""
			var values = new double[1];
			if (BarCount > 0)
				values[BarCount] = 1;
			Plot("C", Close);
			"""
		);

		var results = _engine.Compute(
			Bars(10),
			true,
			[
				new IndicatorRunDto { DefinitionId = IndicatorHelpers.GetScriptId("test.cs") },
				new IndicatorRunDto { DefinitionId = IndicatorHelpers.GetBuiltInId("sma") },
			],
			TimeSpan.FromSeconds(5)
		);

		Assert.NotNull(results[0].Error);
		Assert.Contains("IndexOutOfRange", results[0].Error);
		Assert.StartsWith("line 3:", results[0].Error);
		Assert.Empty(results[0].Plots);
		Assert.Null(results[1].Error);
		Assert.Single(results[1].Plots);
	}

	[Fact]
	public void An_endless_loop_is_stopped_at_the_time_budget()
	{
		Compile(
			"""
			var i = 0;
			while (BarCount > 0) { i++; }
			Plot("C", Close);
			"""
		);

		var started = DateTime.UtcNow;
		var result = Run("test.cs", budget: TimeSpan.FromMilliseconds(200));

		Assert.Contains("longer than", result.Error);
		Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void Endless_recursion_is_stopped_before_the_stack_runs_out()
	{
		Compile(
			"""
			int Deep(int n) => n < 0 ? 0 : 1 + Deep(n + 1);
			Func<int, int> lambda = null;
			lambda = n => 1 + lambda(n + 1);
			if (BarCount > 5)
				Deep(0);
			if (BarCount > 0)
				lambda(0);
			Plot("C", Close);
			"""
		);

		Assert.Contains("Recursion", Run("test.cs", bars: 10).Error);
		Assert.Contains("Recursion", Run("test.cs", bars: 2).Error);
	}

	[Fact]
	public void A_script_that_fails_on_zero_bars_is_marked_on_its_definition()
	{
		var definition = Compile(
			"""
			var last = Close.ToArray()[BarCount - 1];
			Plot("C", Close);
			"""
		);

		Assert.StartsWith("Fails with no bars", definition.Error);
	}

	[Fact]
	public void A_new_version_replaces_the_old_and_a_broken_one_stops_it_running()
	{
		Compile("Plot(\"A\", Close);");
		Assert.Equal("A", Assert.Single(Run("test.cs").Plots).Name);

		Compile("Plot(\"B\", Open);");
		Assert.Equal("B", Assert.Single(Run("test.cs").Plots).Name);

		Compile("Plot(\"B\", Open)");
		var broken = Run("test.cs");
		Assert.NotNull(broken.Error);
		Assert.Empty(broken.Plots);

		_engine.RemoveScript(IndicatorHelpers.GetScriptId("test.cs"));
		Assert.Equal("This indicator is not loaded.", Run("test.cs").Error);
	}

	[Fact]
	public void Two_plots_with_one_name_are_an_error()
	{
		Compile("Plot(\"A\", Close);\nPlot(\"A\", Open);");

		Assert.Contains("two plots named 'A'", Run("test.cs").Error);
	}
}
