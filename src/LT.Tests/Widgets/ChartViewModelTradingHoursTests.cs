using LT.App.Stores;
using LT.App.Widgets.Chart;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Domain.Managers;
using LT.Indicators.Scripting;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// Each chart has its own trading hours: pre- and post-market bars by default (IB useRTH=false),
/// switched from the chart's header (reloading only that chart) and saved with the chart.
/// </summary>
public class ChartViewModelTradingHoursTests : IDisposable
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
	private readonly SettingsManager _settings;
	private readonly List<ChartViewModel> _charts = [];

	public ChartViewModelTradingHoursTests() =>
		_settings = new SettingsManager(_cache.Settings, _ibkr);

	[Fact]
	public void A_new_chart_asks_for_bars_outside_regular_hours()
	{
		var chart = TestUiThread.Invoke(CreateChart);

		Assert.False(chart.UseRegularTradingHours);
		Assert.Equal("ETH", chart.TradingHoursLabel);
		WaitFor(() => _ibkr.HistoricalUseRth.Count > 0);
		Assert.False(_ibkr.HistoricalUseRth[^1]);
	}

	[Fact]
	public void Toggling_one_chart_reloads_it_with_regular_hours_and_leaves_the_others()
	{
		var (first, second) = TestUiThread.Invoke(() => (CreateChart(), CreateChart()));
		WaitFor(() => _ibkr.HistoricalUseRth.Count > 0);

		TestUiThread.Invoke(() => first.ToggleTradingHoursCommand.Execute(null));

		Assert.True(first.UseRegularTradingHours);
		Assert.Equal("RTH", first.TradingHoursLabel);
		Assert.False(second.UseRegularTradingHours);
		WaitFor(() => _ibkr.HistoricalUseRth.Contains(true));
		Assert.Single(_ibkr.HistoricalUseRth, x => x);
		Assert.Null(first.RegularHours);
	}

	[Fact]
	public void Trading_hours_are_saved_with_the_chart_and_restored()
	{
		var placement = new WidgetPlacementDto { Tool = WorkspaceTool.Chart };
		TestUiThread.Invoke(() =>
		{
			var chart = CreateChart();
			chart.ToggleTradingHours();
			chart.SaveState(placement);
		});
		Assert.False(placement.ChartOutsideRegularHours);

		var restored = TestUiThread.Invoke(() =>
		{
			var chart = CreateChart();
			chart.LoadState(placement);
			return chart;
		});

		Assert.True(restored.UseRegularTradingHours);
		WaitFor(() => _ibkr.HistoricalUseRth.Contains(true));
	}

	[Fact]
	public void A_chart_from_a_layout_saved_before_per_chart_hours_shows_outside_regular_hours()
	{
		var chart = TestUiThread.Invoke(() =>
		{
			var chart = CreateChart();
			chart.LoadState(new WidgetPlacementDto { Tool = WorkspaceTool.Chart });
			return chart;
		});

		Assert.False(chart.UseRegularTradingHours);
	}

	[Fact]
	public void The_toggle_is_offered_on_intraday_charts_only()
	{
		var chart = TestUiThread.Invoke(CreateChart);

		TestUiThread.Invoke(() => chart.SetTimeframe(ChartTimeframe.FiveMinutes));
		Assert.True(chart.HasTradingHoursToggle);
		TestUiThread.Invoke(() => chart.SetTimeframe(ChartTimeframe.OneDay));
		Assert.False(chart.HasTradingHoursToggle);
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

	private static void WaitFor(Func<bool> condition)
	{
		var until = DateTime.UtcNow.AddSeconds(5);
		while (!condition())
		{
			Assert.True(DateTime.UtcNow < until, "Timed out");
			Thread.Sleep(10);
		}
	}

	private ChartViewModel CreateChart()
	{
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
			_settings,
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
			new PriceAlertManager(
				new InMemoryPriceAlertRepository(),
				new MarketDataManager(_ibkr, TimeProvider.System),
				new MessageLogManager(),
				_cache.Time
			),
			new ChartDrawingManager(new InMemoryChartDrawingRepository()),
			new ChartDrawingClipboardStore(),
			new AccountStore(_connection, _settings),
			new LinkGroupStore(),
			new DisplayTimeStore(_settings, _cache.Time),
			_cache.Time
		);
		_charts.Add(chart);
		chart.OpenContract(Spy);
		return chart;
	}
}
