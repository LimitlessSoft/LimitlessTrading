using Avalonia.Threading;
using LT.App.Stores;
using LT.App.Widgets.Chart;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Domain.Managers;
using LT.Indicators.Scripting;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// The chart's toolbox bar: shown or hidden per chart (saved with it, shown by default), and its
/// alert tool — armed by its button, it places one alert at the clicked price and disarms; the
/// button again, Esc or hiding the bar disarm it without an alert.
/// </summary>
public class ChartViewModelToolboxTests : IDisposable
{
	private static readonly ContractDto Spy = new()
	{
		ConId = 756733,
		Symbol = "SPY",
		SecurityType = "STK",
		Exchange = "SMART",
		Currency = "USD",
	};

	private readonly TestCache _cache = new();
	private readonly FakeIbkrApiClient _ibkr = new();
	private readonly FakeConnectionManager _connection = new();
	private readonly PriceAlertManager _alerts;
	private readonly List<ChartViewModel> _charts = [];

	public ChartViewModelToolboxTests()
	{
		_alerts = new PriceAlertManager(
			new InMemoryPriceAlertRepository(),
			new MarketDataManager(_ibkr, TimeProvider.System),
			new MessageLogManager(),
			_cache.Time
		);
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
	public void A_new_chart_shows_the_toolbox_and_hiding_it_is_saved_with_the_chart()
	{
		var placement = new WidgetPlacementDto { Tool = WorkspaceTool.Chart };
		TestUiThread.Invoke(() =>
		{
			var chart = CreateChart(Spy);
			Assert.True(chart.ShowToolbox);
			chart.ToggleToolboxCommand.Execute(null);
			chart.SaveState(placement);
		});
		Assert.False(placement.ChartShowToolbox);

		var restored = TestUiThread.Invoke(() =>
		{
			var chart = CreateChart(Spy);
			chart.LoadState(placement);
			return chart;
		});

		Assert.False(restored.ShowToolbox);
	}

	[Fact]
	public void A_layout_saved_before_the_toolbox_shows_it()
	{
		var chart = TestUiThread.Invoke(() =>
		{
			var chart = CreateChart(Spy);
			chart.ShowToolbox = false;
			chart.LoadState(new WidgetPlacementDto { Tool = WorkspaceTool.Chart });
			return chart;
		});

		Assert.True(chart.ShowToolbox);
	}

	[Fact]
	public void The_toolbox_setting_round_trips_with_the_layout()
	{
		var settings = new SettingsManager(_cache.Settings, _ibkr);
		var layout = settings.GetLayout();
		layout.Widgets =
		[
			new WidgetPlacementDto
			{
				Id = "chart-1",
				Tool = WorkspaceTool.Chart,
				ChartShowToolbox = false,
			},
		];

		settings.SaveLayout(layout);

		Assert.False(Assert.Single(settings.GetLayout().Widgets).ChartShowToolbox);
	}

	[Fact]
	public void The_alert_tool_places_one_alert_at_the_clicked_price_and_disarms()
	{
		var chart = TestUiThread.Invoke(() => CreateChart(Spy));
		WaitFor(() => chart.LastPrice != null);
		var tool = Assert.Single(chart.Tools, x => x.Tool == ChartTool.Alert);
		Assert.True(tool.IsEnabled);

		TestUiThread.Invoke(() => chart.SelectToolCommand.Execute(tool));
		Assert.True(chart.IsPlacingAlert);
		Assert.True(tool.IsActive);

		TestUiThread.Invoke(() =>
		{
			chart.PlaceAlert(190);
			Dispatcher.UIThread.RunJobs();
		});

		var alert = Assert.Single(chart.Alerts);
		Assert.Equal(190, alert.Price);
		Assert.Equal(PriceAlertCondition.CrossingUp, alert.Condition);
		Assert.False(chart.IsPlacingAlert);
		Assert.False(tool.IsActive);

		// Disarmed: another click adds nothing
		TestUiThread.Invoke(() => chart.PlaceAlert(170));
		Assert.Single(_alerts.GetAlerts());
	}

	[Fact]
	public void The_button_again_escape_or_hiding_the_bar_disarm_without_an_alert()
	{
		var chart = TestUiThread.Invoke(() => CreateChart(Spy));
		WaitFor(() => chart.LastPrice != null);
		var tool = chart.Tools[0];

		TestUiThread.Invoke(() =>
		{
			chart.SelectTool(tool);
			chart.SelectTool(tool);
		});
		Assert.False(chart.IsPlacingAlert);
		Assert.False(chart.CancelTool());

		TestUiThread.Invoke(() => chart.SelectTool(tool));
		Assert.True(TestUiThread.Invoke(chart.CancelTool));
		Assert.False(chart.IsPlacingAlert);

		TestUiThread.Invoke(() =>
		{
			chart.SelectTool(tool);
			chart.ToggleToolbox();
		});
		Assert.False(chart.IsPlacingAlert);
		Assert.False(tool.IsActive);
		Assert.Empty(_alerts.GetAlerts());
	}

	[Fact]
	public void Without_bars_the_alert_tool_is_disabled_and_does_not_arm()
	{
		_ibkr.HistoricalBars = [];
		var chart = TestUiThread.Invoke(() => CreateChart(Spy));
		var tool = chart.Tools[0];

		TestUiThread.Invoke(() => chart.SelectTool(tool));

		Assert.False(tool.IsEnabled);
		Assert.False(chart.IsPlacingAlert);
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
				new MarketDataManager(_ibkr, TimeProvider.System),
				new InMemoryFootprintTapeRepository(),
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
			new ChartDrawingManager(new InMemoryChartDrawingRepository()),
			new OrderManager(_ibkr, _cache.Manager),
			new ChartDrawingClipboardStore(),
			new OpenOrdersStore(new OrderManager(_ibkr, _cache.Manager), _connection, _cache.Time),
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
