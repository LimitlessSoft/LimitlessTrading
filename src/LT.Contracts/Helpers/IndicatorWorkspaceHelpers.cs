using LT.Contracts.Constants;
using LT.Contracts.Entities;

namespace LT.Contracts.Helpers;

/// <summary>
/// What the indicators folder is set up with: a copy of the SDK and an editor project pointing
/// at it (IntelliSense in Rider, Visual Studio or VS Code), and on first run an example script
/// and a README.
/// </summary>
public static class IndicatorWorkspaceHelpers
{
	public static IReadOnlyList<IndicatorWorkspaceFileEntity> GetFiles(
		string sdkAssemblyPath,
		int runtimeMajorVersion
	)
	{
		var sdk = IndicatorConstants.SdkDirectoryName + "/";
		var files = new List<IndicatorWorkspaceFileEntity>
		{
			new()
			{
				RelativePath = sdk + IndicatorConstants.SdkAssemblyFileName,
				CopyFrom = sdkAssemblyPath,
			},
			new()
			{
				RelativePath = IndicatorConstants.ProjectFileName,
				Content = GetProject(runtimeMajorVersion),
				KeepExisting = true,
			},
			new()
			{
				RelativePath = IndicatorConstants.ReadmeFileName,
				Content = Readme,
				FirstRunOnly = true,
			},
			new()
			{
				RelativePath = IndicatorConstants.ExampleFileName,
				Content = ExampleScript,
				FirstRunOnly = true,
			},
		};
		var documentation = Path.ChangeExtension(sdkAssemblyPath, ".xml");
		files.Insert(
			1,
			new IndicatorWorkspaceFileEntity
			{
				RelativePath = sdk + IndicatorConstants.SdkDocumentationFileName,
				CopyFrom = documentation,
			}
		);
		return files;
	}

	public static string GetProject(int runtimeMajorVersion)
	{
		var usings = string.Join(
			"\n",
			IndicatorConstants.ScriptUsings.Select(x => $"\t\t<Using Include=\"{x}\" />")
		);
		return $"""
			<Project Sdk="Microsoft.NET.Sdk">
				<!-- For your editor only (IntelliSense, go to definition). LimitlessTrading doesn't
				     build this project: it compiles every .cs file in this folder on its own, as one
				     indicator, whenever the file changes. With several scripts the editor reports
				     CS8802 (top-level statements in more than one file): that is expected. -->
				<PropertyGroup>
					<TargetFramework>net{runtimeMajorVersion}.0</TargetFramework>
					<OutputType>Exe</OutputType>
					<ImplicitUsings>disable</ImplicitUsings>
					<Nullable>disable</Nullable>
				</PropertyGroup>

				<ItemGroup>
					<Reference Include="LT.Indicators.Sdk">
						<HintPath>{IndicatorConstants.SdkDirectoryName}/{IndicatorConstants.SdkAssemblyFileName}</HintPath>
					</Reference>
				</ItemGroup>

				<!-- The same usings LimitlessTrading compiles scripts with -->
				<ItemGroup>
			{usings}
					<Using Include="{IndicatorConstants.ScriptStaticUsing}" Static="true" />
				</ItemGroup>

				<ItemGroup>
					<Compile Remove="{IndicatorConstants.SdkDirectoryName}/**;bin/**;obj/**" />
				</ItemGroup>
			</Project>

			""";
	}

	public const string ExampleScript = """
		// EMA cross: a fast and a slow EMA, and a marker where the fast one crosses the slow one.
		// LimitlessTrading compiles this file again whenever you save it and redraws the charts
		// that show it; compile errors show in the chart legend. See README.md for what you can use.

		Name = "EMA cross";
		ShortName = "EMA cross";

		var fastLength = Input("Fast", 9, 1, 500);
		var slowLength = Input("Slow", 21, 1, 500);

		var fast = Ta.Ema(Close, fastLength);
		var slow = Ta.Ema(Close, slowLength);

		Plot("Fast", fast, Colors.Teal);
		Plot("Slow", slow, Colors.Orange);
		Markers("Up", Ta.CrossOver(fast, slow), MarkerShape.TriangleUp, Colors.Green, MarkerPosition.BelowBar);
		Markers("Down", Ta.CrossUnder(fast, slow), MarkerShape.TriangleDown, Colors.Red, MarkerPosition.AboveBar);

		""";

	public const string Readme = """
		# LimitlessTrading indicators

		Every `*.cs` file in this folder is one chart indicator. Edit them in any editor;
		LimitlessTrading compiles a file again as soon as it is saved and redraws every chart that
		shows it. Add one to a chart from its right-click menu: Manage indicators….

		Open this folder in Rider, Visual Studio or VS Code (C# Dev Kit) for IntelliSense:
		`indicators.csproj` references a copy of the SDK in `.sdk/` (refreshed on every start).

		## A script

		Top-level statements, run once over all bars (series-based, not once per bar):

		```csharp
		Pane = PaneKind.Separate;            // Overlay (default), Separate or Volume
		var length = Input("Length", 14, 1, 500);
		var rsi = Ta.Rsi(Close, length);
		Plot("RSI", rsi, Colors.Purple);
		Level("Overbought", 70);
		Level("Oversold", 30);
		```

		A file that is a single expression, e.g. `Ta.Sma(Close, 50)`, is drawn as one line.
		A file may instead declare a class deriving from `Indicator` and override `Compute()`.

		- Bars: `Open`, `High`, `Low`, `Close`, `Volume`, `Hl2`, `Hlc3`, `Ohlc4` (series), `Time`,
		  `BarCount`, `IsIntraday`, `NewSession` (1 on the first bar of each New York day).
		- `Series`: one value per bar, oldest first, NaN = no value. `+ - * /` and `> < >= <=`
		  with series or numbers (conditions are 1 / 0), `& | !`, `Shift(n)` = n bars back,
		  `s[i]`, `Last`, `Length`, `Select`, `Zip`.
		- `Ta`: `Sma Ema Rma Wma Stdev Sum Highest Lowest Change Roc CrossOver CrossUnder
		  TrueRange Atr Rsi Vwap Macd BollingerBands Stoch Max Min Abs Nz Iff`.
		- Inputs: `Input(name, default[, min, max])` for whole numbers, decimals (add a step) and
		  on / off; they show in the indicator's settings on the chart.
		- Drawing: `Plot`, `Histogram`, `ColoredHistogram` (a colour per bar), `Band`, `Markers`,
		  `Level`; colours from `Colors` or `Color.FromHex("#RRGGBB")`.
		- Meta: `Name`, `ShortName`, `Pane`, `ScaleMin` / `ScaleMax`, `Decimals`.

		## What a script can't do

		Scripts compile against the SDK and a minimal part of .NET. Files, network, processes,
		reflection, threads, environment, native code and `unsafe` are rejected when compiling,
		and a run is stopped after 2 seconds. That is a guard against mistakes, not a sandbox:
		scripts run inside LimitlessTrading with your rights, so running someone else's script
		is running their code. Read it first.

		""";
}
