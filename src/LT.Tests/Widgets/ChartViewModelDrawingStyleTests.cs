using Avalonia.Threading;
using LT.App.Stores;
using LT.App.Widgets.Chart;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.ChartDrawings;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Domain.Managers;
using LT.Indicators.Scripting;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// The drawing extras on a chart: the magnet and keep-drawing toggles (saved per chart), undo /
/// redo shared by the symbol's charts, copy / paste across charts and duplicate, per-drawing lock
/// and hide, the style bar (a new style becomes the tool's) and the Fibonacci levels editor.
/// </summary>
public class ChartViewModelDrawingStyleTests : IDisposable
{
	private static readonly ContractDto Spy = new()
	{
		ConId = 756733,
		Symbol = "SPY",
		SecurityType = "STK",
		Exchange = "SMART",
		Currency = "USD",
	};

	private static readonly ContractDto Qqq = new()
	{
		ConId = 320227571,
		Symbol = "QQQ",
		SecurityType = "STK",
		Exchange = "SMART",
		Currency = "USD",
	};

	private readonly TestCache _cache = new();
	private readonly FakeIbkrApiClient _ibkr = new();
	private readonly FakeConnectionManager _connection = new();
	private readonly PriceAlertManager _alerts;
	private readonly InMemoryChartDrawingRepository _repository = new();
	private readonly ChartDrawingManager _drawings;
	private readonly ChartDrawingClipboardStore _clipboard = new();
	private readonly List<ChartViewModel> _charts = [];

	public ChartViewModelDrawingStyleTests()
	{
		LSCoreContainer.EnsureInitialized();
		_alerts = new PriceAlertManager(
			new InMemoryPriceAlertRepository(),
			new MarketDataManager(_ibkr, TimeProvider.System),
			new MessageLogManager(),
			_cache.Time
		);
		_drawings = new ChartDrawingManager(_repository);
		_ibkr.HistoricalBars =
		[
			new BarDto
			{
				Time = _cache.Time.GetUtcNow().AddDays(-1),
				Open = 180,
				High = 186,
				Low = 179,
				Close = 185,
			},
		];
	}

	[Fact]
	public void Magnet_and_keep_drawing_are_saved_with_the_chart_and_off_in_older_layouts()
	{
		var placement = new WidgetPlacementDto { Tool = WorkspaceTool.Chart };
		TestUiThread.Invoke(() =>
		{
			var chart = CreateChart(Spy);
			chart.ToggleDrawingMagnet();
			chart.ToggleKeepDrawingTool();
			Assert.True(Action(chart, "Magnet").IsActive);
			Assert.True(Action(chart, "Keep drawing").IsActive);
			chart.SaveState(placement);
		});
		Assert.True(placement.ChartDrawingMagnet);
		Assert.True(placement.ChartKeepDrawingTool);

		var (restored, old) = TestUiThread.Invoke(() =>
		{
			var restored = CreateChart(Spy);
			restored.LoadState(placement);
			var old = CreateChart(Spy);
			old.DrawingMagnet = true;
			old.LoadState(new WidgetPlacementDto { Tool = WorkspaceTool.Chart });
			return (restored, old);
		});

		Assert.True(restored.DrawingMagnet);
		Assert.True(restored.KeepDrawingTool);
		Assert.False(old.DrawingMagnet);
		Assert.False(old.KeepDrawingTool);
	}

	[Fact]
	public void Keep_drawing_leaves_the_tool_armed_after_each_drawing()
	{
		var chart = ReadyChart();
		TestUiThread.Invoke(() =>
		{
			chart.ToggleKeepDrawingTool();
			chart.SelectDrawingTool(Tool(chart, ChartDrawingKind.TrendLine));
			chart.AddDrawing(ChartDrawingKind.TrendLine, Points(2));
			chart.AddDrawing(ChartDrawingKind.TrendLine, Points(2, price: 190));
			// A note's box: the tool stays too
			chart.EndDrawingTool();
		});

		Assert.Equal(ChartDrawingKind.TrendLine, chart.ActiveDrawingKind);
		Assert.Equal(2, chart.Drawings.Count);
		Assert.Null(chart.SelectedDrawingId);

		TestUiThread.Invoke(() => chart.CancelTool());
		Assert.Null(chart.ActiveDrawingKind);
	}

	[Fact]
	public void Undo_and_redo_step_through_the_symbol_s_drawings_on_every_chart_of_it()
	{
		var chart = ReadyChart();
		var other = TestUiThread.Invoke(() => CreateChart(Spy));
		WaitFor(() => other.CanDraw);
		var undo = Action(chart, "Undo");
		var redo = Action(chart, "Redo");
		Assert.False(undo.IsEnabled);

		var line = TestUiThread.Invoke(() =>
			chart.AddDrawing(ChartDrawingKind.TrendLine, Points(2))
		)!;
		TestUiThread.Invoke(() => chart.UpdateDrawing(line.Id, Points(2, price: 190)));
		Assert.True(undo.IsEnabled);

		// The other chart of SPY undoes the move made here
		TestUiThread.Invoke(() => other.UndoDrawing());
		WaitFor(() => chart.Drawings is [{ Points: [{ Price: 180 }, ..] }]);
		Assert.True(redo.IsEnabled);

		TestUiThread.Invoke(() => chart.UndoDrawing());
		Assert.Empty(chart.Drawings);
		Assert.Null(chart.SelectedDrawingId);

		TestUiThread.Invoke(() =>
		{
			chart.RedoDrawing();
			chart.RedoDrawing();
		});
		Assert.Equal(190, Assert.Single(chart.Drawings).Points[0].Price);
		Assert.False(redo.IsEnabled);
	}

	[Fact]
	public void A_copy_pastes_on_another_chart_offset_and_each_paste_goes_on_from_the_last()
	{
		var chart = ReadyChart();
		var qqq = TestUiThread.Invoke(() => CreateChart(Qqq));
		WaitFor(() => qqq.CanDraw);
		var line = TestUiThread.Invoke(() =>
		{
			var line = chart.AddDrawing(ChartDrawingKind.TrendLine, Points(2))!;
			chart.SetSelectedDrawingColor("#F23645");
			chart.CopySelectedDrawing();
			return line;
		});
		Assert.True(qqq.CanPasteDrawing);
		var bar = ChartTimeframeHelpers.GetBarLength(qqq.SelectedTimeframe.Value);

		var first = TestUiThread.Invoke(() => qqq.PasteDrawing(-2))!;
		var second = TestUiThread.Invoke(() => qqq.PasteDrawing(-2))!;

		Assert.Equal(Qqq.ConId, first.ConId);
		Assert.Equal(
			line.Points[0].Time + bar * ChartDrawingConstants.PasteOffsetBars,
			first.Points[0].Time
		);
		Assert.Equal([178d, 179d], first.Points.Select(x => x.Price));
		Assert.Equal([176d, 177d], second.Points.Select(x => x.Price));
		Assert.Equal("#F23645", second.Style.Color);
		Assert.Equal(second.Id, qqq.SelectedDrawingId);
		Assert.Equal(2, qqq.Drawings.Count);
		Assert.Single(chart.Drawings);
	}

	[Fact]
	public void Duplicate_copies_the_selected_drawing_beside_it_and_leaves_the_clipboard()
	{
		var chart = ReadyChart();
		var note = TestUiThread.Invoke(() =>
			chart.AddDrawing(ChartDrawingKind.Text, Points(1), "Gap")
		)!;

		var copy = TestUiThread.Invoke(() => chart.DuplicateSelectedDrawing(-1))!;

		Assert.NotEqual(note.Id, copy.Id);
		Assert.Equal("Gap", copy.Text);
		Assert.Equal(179, copy.Points[0].Price);
		Assert.Equal(copy.Id, chart.SelectedDrawingId);
		Assert.False(chart.CanPasteDrawing);
	}

	[Fact]
	public void A_locked_drawing_is_restyled_but_not_deleted()
	{
		var chart = ReadyChart();
		TestUiThread.Invoke(() =>
		{
			chart.AddDrawing(ChartDrawingKind.Rectangle, Points(2));
			chart.ToggleSelectedDrawingLocked();
		});
		Assert.True(chart.SelectedDrawingLocked);
		Assert.False(Action(chart, "Delete").IsEnabled);

		TestUiThread.Invoke(() =>
		{
			chart.DeleteSelectedDrawing();
			chart.SetSelectedDrawingLineWidth(chart.DrawingLineWidths[^1]);
		});

		var drawing = Assert.Single(chart.Drawings);
		Assert.Equal(ChartDrawingConstants.MaxLineWidth, drawing.Style.LineWidth);
		Assert.True(chart.DrawingLineWidths[^1].IsActive);

		TestUiThread.Invoke(() => chart.ToggleSelectedDrawingLocked());
		Assert.True(Action(chart, "Delete").IsEnabled);
	}

	[Fact]
	public void A_hidden_drawing_drops_the_selection_and_the_menu_shows_it_again()
	{
		var chart = ReadyChart();
		TestUiThread.Invoke(() =>
		{
			chart.AddDrawing(ChartDrawingKind.TrendLine, Points(2));
			chart.AddDrawing(ChartDrawingKind.Ellipse, Points(2));
			chart.HideSelectedDrawing();
		});

		Assert.Null(chart.SelectedDrawingId);
		Assert.Equal(1, chart.HiddenDrawingCount);

		TestUiThread.Invoke(() => chart.ShowHiddenDrawings());
		Assert.False(chart.HasHiddenDrawings);
	}

	[Fact]
	public void The_style_bar_follows_the_selected_drawing_and_a_new_style_becomes_the_tool_s()
	{
		var chart = ReadyChart();
		Assert.False(chart.IsStyleBarVisible);
		TestUiThread.Invoke(() =>
		{
			chart.AddDrawing(ChartDrawingKind.Arrow, Points(2));
			chart.SetSelectedDrawingLineStyle(
				chart.DrawingLineStyles.Single(x => x.LineStyle == ChartDrawingLineStyle.Dashed)
			);
			chart.SelectedDrawingColorText = "#089981";
			chart.ApplySelectedDrawingColorText();
		});

		Assert.True(chart.IsStyleBarVisible);
		Assert.Equal("#089981", chart.SelectedDrawing!.Style.Color);
		Assert.Equal(ChartDrawingLineStyle.Dashed, chart.SelectedDrawing.Style.LineStyle);
		Assert.False(chart.SelectedDrawingIsFib);

		// The arrow tool's next drawing starts in it (its preview too)
		TestUiThread.Invoke(() => chart.SelectDrawingTool(Tool(chart, ChartDrawingKind.Arrow)));
		Assert.False(chart.IsStyleBarVisible);
		Assert.Equal("#089981", chart.ActiveDrawingStyle!.Color);
		var next = TestUiThread.Invoke(() => chart.AddDrawing(ChartDrawingKind.Arrow, Points(2)))!;
		Assert.Equal(ChartDrawingLineStyle.Dashed, next.Style.LineStyle);
	}

	[Fact]
	public void A_bad_hex_colour_is_refused_with_its_reason()
	{
		var chart = ReadyChart();
		TestUiThread.Invoke(() =>
		{
			chart.AddDrawing(ChartDrawingKind.TrendLine, Points(2));
			chart.SelectedDrawingColorText = "blue";
			chart.ApplySelectedDrawingColorText();
		});

		Assert.Equal("A drawing's colour is a hex colour like #2962FF.", chart.ErrorMessage);
		Assert.Equal(ChartDrawingConstants.DefaultColor, chart.SelectedDrawing!.Style.Color);
	}

	[Fact]
	public void The_fib_editor_adds_removes_and_switches_levels_and_resets_to_the_defaults()
	{
		var chart = ReadyChart();
		TestUiThread.Invoke(() =>
		{
			chart.AddDrawing(ChartDrawingKind.FibRetracement, Points(2));
			chart.BeginEditFibLevels();
		});
		Assert.True(chart.SelectedDrawingIsFib);
		Assert.Equal(ChartDrawingConstants.FibRetracementLevels.Count, chart.FibLevelItems.Count);

		var applied = TestUiThread.Invoke(() =>
		{
			chart.NewFibLevelText = "65%";
			chart.AddFibLevel();
			chart.RemoveFibLevel(chart.FibLevelItems.Single(x => x.ValueText == "0.236"));
			var half = chart.FibLevelItems.Single(x => x.ValueText == "0.5");
			half.IsVisible = false;
			half.SelectedColor = half.Colors.Single(x => x.Hex == "#9C27B0");
			return chart.ApplyFibLevels();
		});

		Assert.True(applied);
		var levels = Assert.Single(chart.Drawings).FibLevels;
		Assert.Equal([0, 0.382, 0.5, 0.618, 0.65, 0.786, 1, 1.618], levels.Select(x => x.Value));
		Assert.False(levels[2].IsVisible);
		Assert.Equal("#9C27B0", levels[2].Color);
		Assert.Null(levels[4].Color);

		TestUiThread.Invoke(() =>
		{
			chart.ResetFibLevels();
			chart.ApplyFibLevels();
		});
		Assert.Equal(
			ChartDrawingConstants.FibRetracementLevels,
			chart.Drawings[0].FibLevels.Select(x => x.Value)
		);
	}

	[Fact]
	public void The_fib_editor_says_why_levels_are_refused()
	{
		var chart = ReadyChart();
		TestUiThread.Invoke(() =>
		{
			chart.AddDrawing(ChartDrawingKind.FibExtension, Points(3));
			chart.BeginEditFibLevels();
			chart.NewFibLevelText = "abc";
			chart.AddFibLevel();
		});
		Assert.NotNull(chart.FibLevelsError);

		var applied = TestUiThread.Invoke(() =>
		{
			chart.FibLevelItems[1].ValueText = "0";
			return chart.ApplyFibLevels();
		});

		Assert.False(applied);
		Assert.Equal("Each Fibonacci level can be there only once.", chart.FibLevelsError);
		Assert.Equal(
			ChartDrawingConstants.FibExtensionLevels,
			chart.Drawings[0].FibLevels.Select(x => x.Value)
		);
	}

	// A SPY chart with bars, in drawing mode
	private ChartViewModel ReadyChart()
	{
		var chart = TestUiThread.Invoke(() => CreateChart(Spy));
		WaitFor(() => chart.CanDraw);
		TestUiThread.Invoke(() => chart.ToggleDrawingMode());
		return chart;
	}

	private static ChartDrawingToolItem Action(ChartViewModel chart, string label) =>
		chart.DrawingToolGroups.SelectMany(x => x.Items).Single(x => x.Label == label);

	public void Dispose()
	{
		TestUiThread.Invoke(() =>
		{
			foreach (var chart in _charts)
				chart.Dispose();
		});
		GC.SuppressFinalize(this);
	}

	private static ChartDrawingToolItem Tool(ChartViewModel chart, ChartDrawingKind kind) =>
		chart.DrawingToolGroups.SelectMany(x => x.Items).Single(x => x.Kind == kind);

	private List<ChartDrawingPointDto> Points(int count, double price = 180) =>
		[
			.. Enumerable
				.Range(0, count)
				.Select(i => new ChartDrawingPointDto
				{
					Time = _cache.Time.GetUtcNow().AddDays(-1).AddMinutes(5 * i),
					Price = price + i,
				}),
		];

	// Runs the UI thread's posted jobs until the condition holds
	private static void WaitFor(Func<bool> condition)
	{
		var until = DateTime.UtcNow.AddSeconds(5);
		while (true)
		{
			TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());
			if (condition())
				return;
			Assert.True(DateTime.UtcNow < until, "Timed out");
			Thread.Sleep(10);
		}
	}

	private ChartViewModel CreateChart(ContractDto contract)
	{
		var settings = new SettingsManager(_cache.Settings, _ibkr);
		var chart = new ChartViewModel(
			new ChartManager(
				_ibkr,
				_cache.Manager,
				new MarketDataManager(_ibkr, TimeProvider.System),
				_cache.Time
			),
			new FootprintManager(
				_ibkr,
				_cache.Manager,
				new MarketDataManager(_ibkr, TimeProvider.System)
			),
			new ContractManager(_ibkr, _cache.Settings, _cache.Manager),
			_connection,
			new MessageLogManager(),
			settings,
			new TradeJournalManager(
				_ibkr,
				new AccountManager(_ibkr, _cache.Manager),
				new InMemoryTradeJournalRepository(),
				_cache.Time
			),
			new PositionManager(_ibkr, _cache.Manager),
			new IndicatorManager(
				new IndicatorEngine(),
				new InMemoryIndicatorScriptRepository(),
				new MessageLogManager()
			),
			new ChartTemplateManager(_cache.Settings),
			_alerts,
			_drawings,
			_clipboard,
			new AccountStore(_connection, settings),
			new LinkGroupStore(),
			new DisplayTimeStore(settings, _cache.Time),
			_cache.Time
		);
		_charts.Add(chart);
		chart.OpenContract(contract);
		return chart;
	}
}
