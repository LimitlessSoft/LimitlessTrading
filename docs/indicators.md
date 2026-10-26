# Chart indicators

Charts draw indicators: built-in ones (SMA, EMA, VWAP, Bollinger Bands, RSI, MACD, ATR, volume
moving average) and the user's own, written as C# script files in any editor and recompiled the
moment they are saved. Both are written against the same SDK, `LT.Indicators.Sdk`.

## Using them on a chart

- **Manage:** right-click the chart → **Manage indicators…** (`ChartView`) opens the indicator
  manager at the pointer. At the top, the chart's indicators by their legend label, each with ✕
  to remove it at once (the manager stays open). Below, the picker: built-ins first, then
  scripts, searchable by name or file name (Enter adds the first match). A script that doesn't
  compile is listed with its error. "Open the scripts folder…" opens the folder in the file
  manager. At most `IndicatorConstants.MaxIndicatorsPerChart` (16) per chart; a full chart shows
  the limit in place of the search and list, removal still works.
- **Legend:** every pane has one at its top left — overlays under the OHLC readout, volume
  indicators on the volume bars, each separate indicator in its own pane. A row shows the short
  name and inputs ("BB 20 2"), the value of each line / histogram / band at the hovered bar in
  its colour, or ⚠ and the error. Hovering a row shows its buttons: eye (hide / show; a hidden
  indicator keeps its row and pane), gear (settings) and ✕ (remove). Clicking the name also
  opens the settings.
- **Settings:** a dialog generated from the inputs the indicator declares — a spin box per
  number (whole or decimal, within its range and step), a check box per on / off input,
  Defaults / Cancel / OK. A script also shows its path with "Edit script" (opens it with the
  system's editor) and its compile errors. OK goes through
  `IIndicatorManager.UpdateInputs` (validated, `IndicatorValidationCodes`).
- **Saved** per chart in the layout (`WidgetPlacementDto.ChartIndicators`: instance id,
  definition id, input values, visibility, optional plot colours). Layouts from before
  indicators have none. A saved value that no longer fits the definition (the script changed its
  range or dropped the input) falls back to the default (`IndicatorHelpers.ResolveInputs`).
- **Templates:** a chart's indicators can be saved with its timeframe as a chart template and
  applied to other charts, exported and imported — see
  [ui-patterns.md → Chart templates](ui-patterns.md#chart-templates).

## How it is built

```
LT.Indicators.Sdk          Series, Ta, Indicator, Script (static facade), Color(s) — no deps
LT.Indicators.Scripting    IndicatorEngine : IIndicatorEngine, built-ins, ScriptCompiler (Roslyn)
LT.Repository              IndicatorScriptRepository : IIndicatorScriptRepository (files, watcher)
LT.Domain                  IndicatorManager : IIndicatorManager (catalog, validation, compute)
LT.Widgets.Chart           CandlestickChart panes + legend, picker, settings dialog
```

- The SDK types live only in `LT.Indicators.Sdk` and `LT.Indicators.Scripting`, the way IBKR.Net
  lives only in `LT.Ibkr`: everything crossing `IIndicatorEngine` is an `LT.Contracts` DTO
  (`IndicatorDefinitionDto`, `IndicatorResultDto`, `IndicatorPlotDto`; mappers in
  `LT.Indicators.Scripting/DtoMappings`).
- `IndicatorManager` (singleton) holds the catalog. `IndicatorScriptHost` (hosted service) runs
  its `RunAsync` for the app's lifetime: it sets up the scripts folder, compiles every script and
  recompiles a file whenever the repository reports it changed — one compile at a time on the
  thread pool. Compile errors land on the definition (`Error`, `Diagnostics` with line and
  column) and in Messages (source Indicators); `DefinitionsChanged` tells open charts.
- **Compute** is series-based: an indicator runs once over all bars and returns whole series.
  The chart view model calls `IIndicatorManager.Compute` on the thread pool when the bars, its
  indicator list or a script it uses change — throttled to one compute per
  `IndicatorConstants.ComputeDebounce` (200 ms), one at a time, a change while it runs computes
  again after it — and posts the results to the UI thread. A different series (new contract or
  timeframe) drops the old plots at once so they never sit on the wrong bars. Every indicator runs
  in a fresh instance; whatever it throws becomes its result's `Error` and the others still draw.
- **Panes** (`CandlestickChart`, `ChartPane`): price chart with the volume bars at its bottom on
  top, then one pane per separate indicator (`IndicatorChartHelpers.GetPaneHeight`: 20 % each,
  60 % at most together). All share the horizontal viewport (zoom, pan, right margin,
  crosshair); each has its own vertical scale — overlays widen the price scale like the bars do,
  a separate pane uses the indicator's fixed `ScaleMin` / `ScaleMax` (RSI 0 … 100) or autoscales
  the visible values. The crosshair labels the value of the pane under the pointer.

## Script files

They live in the `indicators` folder of the app-data folder (`~/.config/LimitlessTrading/indicators`
on Linux and macOS, `%APPDATA%\LimitlessTrading\indicators` on Windows), one indicator per `*.cs`
file; files starting with `.` or `~` are ignored. Its id is `script:<file name>`, so renaming a
file is a new indicator.

On first run (no folder yet) the app writes an example (`ema-cross.cs`) and a `README.md`. On every
start it refreshes `.sdk/LT.Indicators.Sdk.dll` (+ its XML docs) and writes `indicators.csproj`
if it is missing (a user's edits are kept). The project references the SDK copy and has the same
usings scripts are compiled with, so opening the folder in Rider, Visual Studio or VS Code gives
IntelliSense and docs. LT never builds that project. Known editor quirk: with several scripts the
editor reports CS8802 ("only one compilation unit can have top-level statements") — expected,
since LT compiles each file on its own.

The folder is watched with a `FileSystemWatcher`; changes are collected and reported once they
have been quiet for 300 ms (`ReloadDebounce`; editors save in several steps). When the watcher
can't run or errors (the Linux inotify limit, network drives, buffer overflow) the folder is
scanned every 2 s instead (`PollInterval`). A file whose source didn't change isn't recompiled
(builds are cached in memory by a hash of the source and the SDK; there is no disk cache).

### Format

Top-level statements — they become the body of `Compute()` of a generated `Indicator` subclass:

```csharp
Name = "EMA cross";                     // list name; default: the file name
ShortName = "EMA cross";                // legend name before the inputs
Pane = PaneKind.Overlay;                // Overlay (default), Separate or Volume

var fast = Ta.Ema(Close, Input("Fast", 9, 1, 500));
var slow = Ta.Ema(Close, Input("Slow", 21, 1, 500));

Plot("Fast", fast, Colors.Teal);
Plot("Slow", slow, Colors.Orange);
Markers("Up", Ta.CrossOver(fast, slow), MarkerShape.TriangleUp, Colors.Green, MarkerPosition.BelowBar);
```

- A file that is **one expression** (`Ta.Sma(Close, 50)`, optional `;`) is drawn as one line named
  after the file.
- A file **without statements** must declare exactly one public, non-abstract class deriving
  from `Indicator` with a parameterless constructor, overriding `protected void Compute()`.
- Helper functions (local functions) and types (classes, records) may follow the statements.
  `using` directives at the top are kept (and checked against the ban list).
- `Compute` also runs once on **zero bars** when the file is loaded — that is how its name,
  inputs and plots are learnt (Pine-style declaration by running). It must not fail on empty
  series; every `Ta` function handles them. A script that does fails with "Fails with no bars".

### SDK surface

| | |
|---|---|
| Bars | `Open High Low Close Volume Hl2 Hlc3 Ohlc4` (series), `Time` (bar start, UTC; daily bars at midnight UTC), `BarCount`, `IsIntraday`, `NewSession` (1 on the first bar of each New York trading day intraday, else only the first bar) |
| `Series` | one double per bar, oldest first, NaN = no value. `+ - * / %` with series or numbers (÷0 → NaN), `> < >= <=` → conditions (1 / 0 / NaN), `& \| !`, `EqualTo`, `Shift(n)` (n bars back), `s[i]` (NaN outside), `Last`, `Length`, `Select`, `Zip`, `ToArray`, `Series.Constant/Create/Empty`, implicit from `double[]` |
| `Ta` | `Sma Ema Rma Wma Stdev Sum Highest Lowest Change Roc CrossOver CrossUnder TrueRange Atr Rsi Vwap Macd BollingerBands Stoch Max Min Abs Nz Iff` |
| Inputs | `Input(name, int default, min, max)`, `Input(name, double default, min, max, step)`, `Input(name, bool default)`; a value out of range is clamped |
| Drawing | `Plot(name, series, color, width, LineStyle)`, `Histogram(name, series, color, negativeColor)`, `Band(name, upper, lower, color, fillOpacity)`, `Markers(name, condition, MarkerShape, color, MarkerPosition, at)`, `Level(name, value, color, LineStyle)`; plot names are unique per indicator; plots without a colour take `Colors.Palette` in turn |
| Meta | `Name`, `ShortName`, `Pane` (`PaneKind`), `ScaleMin`, `ScaleMax`, `Decimals` |
| Colours | `Colors.*`, `Color.FromHex("#RRGGBB[AA]")`, `Color.FromRgb`, `WithOpacity` — no Avalonia dependency |

Conventions (documented in `Ta`): moving windows are NaN while any value in the window is NaN;
**EMA and RMA are seeded with the SMA of their first `length` values** (TradingView's and
StockCharts' convention; the first EMA value is at bar `length − 1`), after which a NaN input
gives a NaN output and leaves the average unchanged. RSI is Wilder's (RMA of gains / losses,
first value at bar `length`; 100 without losses, 0 without gains). True range of the first bar is
high − low. `Stdev` is the population deviation. Unit tests check RSI, EMA and SMA against
StockCharts' published tables.

The `Script` class is the same surface as statics (`using static LT.Indicators.Sdk.Script` is
one of the usings): it is what an editor resolves `Close` / `Plot` through, and what helper
classes in a script use; it acts on the indicator computing on the current thread.

## What a script can't do — and what that doesn't mean

`ScriptCompiler` compiles with Roslyn's `CSharpCompilation` (not the scripting API) against an
allowlist of references only: `System.Private.CoreLib`, `System.Runtime`, `System.Collections`,
`System.Linq` and the SDK. `allowUnsafe` is off. Then `ScriptSecurityWalker` resolves every name,
expression, attribute and using directive with the semantic model and rejects, as compile
errors on their line:

- namespaces `System.IO`, `System.Net`, `System.Reflection`, `System.Runtime.*` (interop,
  `Unsafe`, loaders, …), `System.Threading.*` (threads, tasks, timers, locks), `System.Diagnostics`
  (processes), `System.Security`, `System.Resources`, `System.Timers`, `System.CodeDom`,
  `Microsoft.Win32`;
- types `Environment`, `AppDomain`, `AppContext`, `Activator`, `Type` (so `typeof(…).Assembly`
  and `GetType()`), `Delegate` / `MulticastDelegate` members, `GC`, `Console`, `Buffer`, runtime
  handles, `TypedReference`, `ArgIterator`; also when they are only an expression's type, a
  generic argument or an array element;
- `extern` / `unsafe` / `async` members, `DllImport`, pointers, `fixed`, `lock`, `await`,
  finalizers, `__makeref` & co., `extern alias`, `dynamic`.

`ScriptCheckpointRewriter` then puts `IndicatorRuntime.Checkpoint()` at the top of every loop
body, before every `goto` and at the start of every method, local function, accessor and lambda
(expression bodies become blocks). It throws once the run has used up its budget
(`IndicatorConstants.ComputeBudget`, 2 s, which also covers the constructor) and,
via `RuntimeHelpers.EnsureSufficientExecutionStack`, before deep recursion runs out of stack — an
endless loop or recursion stops that indicator ("ran longer than 2 s", "Recursion too deep")
instead of hanging or crashing the app. Scripts are emitted unoptimised with a PDB and `#line`
mapping, so a runtime error names the file's line ("line 3: IndexOutOfRangeException: …").
Each build loads into its own collectible `AssemblyLoadContext`, unloaded when the file changes
or is deleted; a file that stops compiling stops running (its last good build is dropped).

**This is not a sandbox.** Scripts run in-process with the user's rights. The ban list stops
mistakes and the obvious ways to touch files, network, processes or the runtime, but a
determined script author may find a way around it (allocation bombs, an API we didn't think of,
a bug in the walker). **Running someone else's script is running their code**: read it first.
A real sandbox (separate process, WASM) is not built.

## Gaps

- No in-app editor (by choice: an external editor with hot reload); no per-plot colour picker in
  the settings dialog (`IndicatorInstanceDto.PlotColors` is saved and applied, nothing sets it
  yet); pane heights can't be dragged; no "source" input type (close / hl2 …) or enum inputs.
- Indicators compute over the loaded bars only (no extra history is requested for warm-up).
- Compiled builds are cached in memory only; every start compiles each script once (~1 s for
  the first, much less after).
