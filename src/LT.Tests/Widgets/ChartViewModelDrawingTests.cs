using Avalonia.Threading;
using LT.App.Stores;
using LT.App.Widgets.Chart;
using LT.Contracts.Dtos.ChartDrawings;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Domain.Managers;
using LT.Indicators.Scripting;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// Drawing mode: the toolbox's Draw button shows the palette (saved per chart); a tool is armed
/// only in drawing mode and never with the alert tool; a finished drawing is kept for the contract
/// (every chart of it shows it), selected, and the cursor armed again; Delete, Remove all, lock and
/// hide act on the drawings; Esc steps back.
/// </summary>
public class ChartViewModelDrawingTests : IDisposable
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
	private readonly List<ChartViewModel> _charts = [];

	public ChartViewModelDrawingTests()
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
	public void Drawing_mode_lock_and_hide_are_saved_with_the_chart()
	{
		var placement = new WidgetPlacementDto { Tool = WorkspaceTool.Chart };
		TestUiThread.Invoke(() =>
		{
			var chart = CreateChart(Spy);
			Assert.False(chart.IsDrawingMode);
			chart.DrawingModeItem.Command.Execute(chart.DrawingModeItem);
			Assert.True(chart.DrawingModeItem.IsActive);
			chart.ToggleDrawingsLocked();
			chart.ToggleDrawingsHidden();
			chart.SaveState(placement);
		});
		Assert.True(placement.ChartDrawingMode);
		Assert.True(placement.ChartDrawingsLocked);
		Assert.True(placement.ChartDrawingsHidden);

		var restored = TestUiThread.Invoke(() =>
		{
			var chart = CreateChart(Spy);
			chart.LoadState(placement);
			return chart;
		});

		Assert.True(restored.IsDrawingMode);
		Assert.True(restored.DrawingsLocked);
		Assert.True(restored.DrawingsHidden);
	}

	[Fact]
	public void A_layout_saved_before_drawing_mode_has_it_off()
	{
		var chart = TestUiThread.Invoke(() =>
		{
			var chart = CreateChart(Spy);
			chart.IsDrawingMode = true;
			chart.LoadState(new WidgetPlacementDto { Tool = WorkspaceTool.Chart });
			return chart;
		});

		Assert.False(chart.IsDrawingMode);
		Assert.False(chart.DrawingsLocked);
		Assert.False(chart.DrawingsHidden);
	}

	[Fact]
	public void The_palette_holds_every_kind_once_after_the_cursor()
	{
		var chart = TestUiThread.Invoke(() => CreateChart(Spy));

		var tools = chart.DrawingToolGroups.SelectMany(x => x.Items).Where(x => x.IsTool).ToList();

		Assert.Null(tools[0].Kind);
		Assert.Equal(
			Enum.GetValues<ChartDrawingKind>().Order(),
			tools.Skip(1).Select(x => x.Kind!.Value).Order()
		);
		Assert.All(
			chart.DrawingToolGroups.SelectMany(x => x.Items),
			x => Assert.NotEmpty(x.ToolTip)
		);
	}

	[Fact]
	public void A_tool_arms_only_in_drawing_mode_and_never_with_the_alert_tool()
	{
		var chart = TestUiThread.Invoke(() => CreateChart(Spy));
		WaitFor(() => chart.CanDraw);
		var trend = Tool(chart, ChartDrawingKind.TrendLine);

		TestUiThread.Invoke(() => chart.SelectDrawingTool(trend));
		Assert.Null(chart.ActiveDrawingKind);

		TestUiThread.Invoke(() =>
		{
			chart.ToggleDrawingMode();
			chart.SelectDrawingTool(trend);
		});
		Assert.Equal(ChartDrawingKind.TrendLine, chart.ActiveDrawingKind);
		Assert.True(trend.IsActive);

		TestUiThread.Invoke(() => chart.SelectTool(chart.Tools[0]));
		Assert.True(chart.IsPlacingAlert);
		Assert.Null(chart.ActiveDrawingKind);
		Assert.False(trend.IsActive);

		TestUiThread.Invoke(() => chart.SelectDrawingTool(trend));
		Assert.False(chart.IsPlacingAlert);
		Assert.Equal(ChartDrawingKind.TrendLine, chart.ActiveDrawingKind);

		// The armed tool's button again: the cursor
		TestUiThread.Invoke(() => chart.SelectDrawingTool(trend));
		Assert.Null(chart.ActiveDrawingKind);
	}

	[Fact]
	public void A_finished_drawing_is_kept_for_the_contract_selected_and_the_cursor_armed()
	{
		var chart = TestUiThread.Invoke(() => CreateChart(Spy));
		var other = TestUiThread.Invoke(() => CreateChart(Spy));
		WaitFor(() => chart.CanDraw && other.CanDraw);
		TestUiThread.Invoke(() =>
		{
			chart.ToggleDrawingMode();
			chart.SelectDrawingTool(Tool(chart, ChartDrawingKind.Rectangle));
		});

		var created = TestUiThread.Invoke(() =>
			chart.AddDrawing(ChartDrawingKind.Rectangle, Points(2))
		);

		Assert.NotNull(created);
		Assert.Equal(created.Id, Assert.Single(chart.Drawings).Id);
		Assert.Equal(created.Id, chart.SelectedDrawingId);
		Assert.Null(chart.ActiveDrawingKind);
		Assert.Equal(Spy.ConId, Assert.Single(_repository.Drawings).ConId);
		// The other chart of SPY hears of it on the UI thread
		WaitFor(() => other.Drawings.Count == 1);
	}

	[Fact]
	public void A_refused_drawing_shows_why_and_adds_nothing()
	{
		var chart = TestUiThread.Invoke(() => CreateChart(Spy));
		WaitFor(() => chart.CanDraw);

		var created = TestUiThread.Invoke(() =>
			chart.AddDrawing(ChartDrawingKind.Text, Points(1), " ")
		);

		Assert.Null(created);
		Assert.Empty(chart.Drawings);
		Assert.False(string.IsNullOrEmpty(chart.ErrorMessage));
	}

	[Fact]
	public void A_moved_drawing_keeps_its_new_points()
	{
		var chart = TestUiThread.Invoke(() => CreateChart(Spy));
		WaitFor(() => chart.CanDraw);
		var line = TestUiThread.Invoke(() =>
			chart.AddDrawing(ChartDrawingKind.TrendLine, Points(2))
		)!;

		TestUiThread.Invoke(() => chart.UpdateDrawing(line.Id, Points(2, price: 190)));

		Assert.Equal([190d, 191d], Assert.Single(chart.Drawings).Points.Select(x => x.Price));
	}

	[Fact]
	public void Delete_removes_the_selected_drawing_but_not_while_locked()
	{
		var chart = TestUiThread.Invoke(() => CreateChart(Spy));
		WaitFor(() => chart.CanDraw);
		TestUiThread.Invoke(() => chart.ToggleDrawingMode());
		var first = TestUiThread.Invoke(() =>
			chart.AddDrawing(ChartDrawingKind.TrendLine, Points(2))
		)!;
		var second = TestUiThread.Invoke(() =>
			chart.AddDrawing(ChartDrawingKind.Ellipse, Points(2))
		)!;
		var delete = chart.DrawingToolGroups[^1].Items[0];
		Assert.True(delete.IsEnabled);

		TestUiThread.Invoke(() =>
		{
			chart.ToggleDrawingsLocked();
			chart.SelectedDrawingId = first.Id;
			chart.DeleteSelectedDrawing();
		});
		Assert.Equal(2, chart.Drawings.Count);
		Assert.False(delete.IsEnabled);

		TestUiThread.Invoke(() =>
		{
			chart.ToggleDrawingsLocked();
			chart.SelectedDrawingId = first.Id;
			chart.DeleteSelectedDrawing();
		});
		Assert.Equal(second.Id, Assert.Single(chart.Drawings).Id);
		Assert.Null(chart.SelectedDrawingId);
	}

	[Fact]
	public void Locking_drops_the_selection()
	{
		var chart = TestUiThread.Invoke(() => CreateChart(Spy));
		WaitFor(() => chart.CanDraw);
		TestUiThread.Invoke(() => chart.AddDrawing(ChartDrawingKind.TrendLine, Points(2)));
		Assert.NotNull(chart.SelectedDrawingId);

		TestUiThread.Invoke(() => chart.ToggleDrawingsLocked());

		Assert.Null(chart.SelectedDrawingId);
	}

	[Fact]
	public void Remove_all_asks_first_and_removes_only_this_symbol_s()
	{
		_repository.Drawings =
		[
			new()
			{
				Id = "qqq",
				ConId = Qqq.ConId,
				Kind = ChartDrawingKind.HorizontalLine,
				Points = [new() { Time = _cache.Time.GetUtcNow(), Price = 400 }],
			},
		];
		var chart = TestUiThread.Invoke(() => CreateChart(Spy));
		WaitFor(() => chart.CanDraw);
		TestUiThread.Invoke(() =>
		{
			chart.AddDrawing(ChartDrawingKind.TrendLine, Points(2));
			chart.AddDrawing(ChartDrawingKind.Rectangle, Points(2));
			chart.AskRemoveAllDrawings();
		});
		Assert.True(chart.IsConfirmingRemoveAllDrawings);
		Assert.Equal("Remove all 2 drawings of SPY?", chart.RemoveAllDrawingsPrompt);

		TestUiThread.Invoke(() => chart.CancelRemoveAllDrawings());
		Assert.Equal(2, chart.Drawings.Count);

		TestUiThread.Invoke(() =>
		{
			chart.AskRemoveAllDrawings();
			chart.ConfirmRemoveAllDrawings();
		});
		Assert.Empty(chart.Drawings);
		Assert.False(chart.IsConfirmingRemoveAllDrawings);
		Assert.Equal("qqq", Assert.Single(_repository.Drawings).Id);
	}

	[Fact]
	public void Arming_a_tool_shows_hidden_drawings_again()
	{
		var chart = TestUiThread.Invoke(() => CreateChart(Spy));
		WaitFor(() => chart.CanDraw);
		TestUiThread.Invoke(() =>
		{
			chart.ToggleDrawingMode();
			chart.AddDrawing(ChartDrawingKind.TrendLine, Points(2));
			chart.ToggleDrawingsHidden();
		});
		Assert.True(chart.DrawingsHidden);
		Assert.Null(chart.SelectedDrawingId);

		TestUiThread.Invoke(() => chart.SelectDrawingTool(Tool(chart, ChartDrawingKind.Arrow)));

		Assert.False(chart.DrawingsHidden);
	}

	[Fact]
	public void Escape_goes_back_to_the_cursor_then_drops_the_selection()
	{
		var chart = TestUiThread.Invoke(() => CreateChart(Spy));
		WaitFor(() => chart.CanDraw);
		var line = TestUiThread.Invoke(() =>
		{
			chart.ToggleDrawingMode();
			var line = chart.AddDrawing(ChartDrawingKind.TrendLine, Points(2))!;
			chart.SelectDrawingTool(Tool(chart, ChartDrawingKind.Brush));
			return line;
		});

		Assert.True(TestUiThread.Invoke(chart.CancelTool));
		Assert.Null(chart.ActiveDrawingKind);

		TestUiThread.Invoke(() => chart.SelectedDrawingId = line.Id);
		Assert.True(TestUiThread.Invoke(chart.CancelTool));
		Assert.Null(chart.SelectedDrawingId);
		Assert.False(TestUiThread.Invoke(chart.CancelTool));
		Assert.Single(chart.Drawings);
	}

	[Fact]
	public void Turning_drawing_mode_off_puts_the_tool_down_and_keeps_the_drawings()
	{
		var chart = TestUiThread.Invoke(() => CreateChart(Spy));
		WaitFor(() => chart.CanDraw);
		TestUiThread.Invoke(() =>
		{
			chart.ToggleDrawingMode();
			chart.AddDrawing(ChartDrawingKind.TrendLine, Points(2));
			chart.SelectDrawingTool(Tool(chart, ChartDrawingKind.Triangle));
			chart.ToggleDrawingMode();
		});

		Assert.Null(chart.ActiveDrawingKind);
		Assert.Null(chart.SelectedDrawingId);
		Assert.Single(chart.Drawings);
	}

	[Fact]
	public void The_drawings_follow_the_charted_contract()
	{
		_repository.Drawings =
		[
			new()
			{
				Id = "qqq",
				ConId = Qqq.ConId,
				Kind = ChartDrawingKind.HorizontalLine,
				Points = [new() { Time = _cache.Time.GetUtcNow(), Price = 400 }],
			},
		];
		var chart = TestUiThread.Invoke(() => CreateChart(Spy));
		WaitFor(() => chart.CanDraw);
		TestUiThread.Invoke(() => chart.AddDrawing(ChartDrawingKind.TrendLine, Points(2)));

		TestUiThread.Invoke(() => chart.OpenContract(Qqq));

		Assert.Equal("qqq", Assert.Single(chart.Drawings).Id);
		Assert.Null(chart.SelectedDrawingId);
	}

	[Fact]
	public void Without_bars_the_drawing_tools_are_disabled_and_do_not_arm()
	{
		_ibkr.HistoricalBars = [];
		var chart = TestUiThread.Invoke(() => CreateChart(Spy));
		var trend = Tool(chart, ChartDrawingKind.TrendLine);

		TestUiThread.Invoke(() =>
		{
			chart.ToggleDrawingMode();
			chart.SelectDrawingTool(trend);
		});

		Assert.False(trend.IsEnabled);
		Assert.Null(chart.ActiveDrawingKind);
		Assert.Null(
			TestUiThread.Invoke(() => chart.AddDrawing(ChartDrawingKind.TrendLine, Points(2)))
		);
	}

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
