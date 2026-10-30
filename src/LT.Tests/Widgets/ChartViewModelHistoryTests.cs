using Avalonia.Threading;
using LT.App.Stores;
using LT.App.Widgets.Chart;
using LT.Contracts.Constants;
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
/// The period is the window the chart shows: picking one fits the view to it (reloading only
/// when the bars don't reach back to its start), Today on intraday bars being the exchange's
/// whole session in the chart's trading hours; zooming away shows "Custom" until it is picked
/// again. Scrolling to the oldest bar loads the page of history before it, one at a time, until
/// IB has no more.
/// </summary>
public class ChartViewModelHistoryTests : IDisposable
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
	private readonly MessageLogManager _messages = new();
	private readonly SettingsManager _settings;
	private readonly List<ChartViewModel> _charts = [];

	public ChartViewModelHistoryTests()
	{
		_settings = new SettingsManager(_cache.Settings, _ibkr);
		// The latest bars: three days back and half an hour ago
		_ibkr.HistoricalBy = (_, _) =>
			Task.FromResult<IReadOnlyList<BarDto>>([
				Bar(Now.AddDays(-3)),
				Bar(Now.AddMinutes(-30)),
			]);
	}

	private DateTimeOffset Now => _cache.Time.GetUtcNow();

	[Fact]
	public void Zooming_away_shows_custom_and_picking_the_period_again_fits_it()
	{
		var chart = TestUiThread.Invoke(CreateChart);
		WaitFor(() => chart.Bars.Count == 2);
		var resets = 0;
		TestUiThread.Invoke(() =>
		{
			chart.SetPeriod(ChartPeriod.Today);
			chart.ViewResetRequested += () => resets++;
			Assert.Equal("Today", chart.PeriodLabel);

			// The chart reports the wheel zoomed the view away from the period
			chart.IsPeriodFitted = false;
			Assert.True(chart.IsPeriodCustom);
			Assert.Equal("Custom", chart.PeriodLabel);

			var calls = _ibkr.HistoricalCalls;
			chart.SetPeriod(ChartPeriod.Today);

			Assert.Equal(1, resets);
			Assert.False(chart.IsPeriodCustom);
			Assert.Equal("Today", chart.PeriodLabel);
			Assert.Equal(calls, _ibkr.HistoricalCalls);
		});
	}

	[Fact]
	public void Auto_is_never_custom()
	{
		var chart = TestUiThread.Invoke(CreateChart);
		TestUiThread.Invoke(() =>
		{
			chart.IsPeriodFitted = false;

			Assert.False(chart.IsPeriodCustom);
			Assert.Equal("Auto", chart.PeriodLabel);
		});
	}

	[Fact]
	public void The_saved_period_is_the_chosen_one_not_custom()
	{
		var chart = TestUiThread.Invoke(CreateChart);
		TestUiThread.Invoke(() =>
		{
			chart.SetPeriod(ChartPeriod.ThisWeek);
			chart.IsPeriodFitted = false;
			var placement = new WidgetPlacementDto();

			chart.SaveState(placement);

			Assert.Equal(ChartPeriod.ThisWeek, placement.ChartPeriod);
		});
	}

	[Fact]
	public void A_period_the_loaded_bars_cover_zooms_without_reloading()
	{
		var chart = TestUiThread.Invoke(CreateChart);
		WaitFor(() => chart.Bars.Count == 2);
		var resets = 0;
		TestUiThread.Invoke(() =>
		{
			chart.ViewResetRequested += () => resets++;
			var bars = chart.Bars;

			chart.SetPeriod(ChartPeriod.Today);

			Assert.Equal(1, _ibkr.HistoricalCalls);
			Assert.Same(bars, chart.Bars);
			Assert.Equal(
				ChartPeriodHelpers.GetStart(ChartPeriod.Today, Now, chart.DisplayTime.Zone),
				chart.PeriodStart
			);
			Assert.Equal(1, resets);
		});
	}

	[Fact]
	public void Today_is_the_exchanges_whole_session_in_the_charts_trading_hours()
	{
		_ibkr.SearchResults = [SpyDetails()];
		var chart = TestUiThread.Invoke(CreateChart);
		TestUiThread.Invoke(() => chart.SetTimeframe(ChartTimeframe.FiveMinutes));
		WaitFor(() => chart.Bars.Count == 2 && !chart.IsLoading);
		var calls = _ibkr.HistoricalCalls;

		TestUiThread.Invoke(() =>
		{
			chart.SetPeriod(ChartPeriod.Today);

			// Pre- and post-market shown: 04:00 to 20:00 in New York, laid out whole, no reload
			Assert.Equal(calls, _ibkr.HistoricalCalls);
			Assert.Equal(Utc(10, 23, 8, 0), chart.PeriodStart);
			Assert.Equal(192, chart.PeriodSlotTimes!.Count);
			chart.ToggleTradingHours();
		});

		WaitFor(() => _ibkr.HistoricalCalls == calls + 1 && !chart.IsLoading);
		TestUiThread.Invoke(() =>
		{
			// Regular hours only: 09:30 to 16:00
			Assert.Equal(Utc(10, 23, 13, 30), chart.PeriodStart);
			Assert.Equal(78, chart.PeriodSlotTimes!.Count);
			Assert.Equal(Utc(10, 23, 19, 55), chart.PeriodSlotTimes[^1]);
		});
	}

	[Fact]
	public void The_next_trading_days_first_bar_moves_today_on()
	{
		_ibkr.SearchResults = [SpyDetails()];
		var chart = TestUiThread.Invoke(CreateChart);
		TestUiThread.Invoke(() => chart.SetTimeframe(ChartTimeframe.FiveMinutes));
		WaitFor(() => chart.Bars.Count == 2 && !chart.IsLoading);

		TestUiThread.Invoke(() =>
		{
			chart.SetPeriod(ChartPeriod.Today);
			Assert.Equal(Utc(10, 23, 8, 0), chart.PeriodStart);

			// Monday 04:00 in New York: the pre-market's first bar comes in
			_cache.Time.SetUtcNow(Utc(10, 26, 8, 0).AddSeconds(5));
			chart.Bars = [.. chart.Bars, Bar(Utc(10, 26, 8, 0))];

			Assert.Equal(Utc(10, 26, 8, 0), chart.PeriodStart);
			Assert.Equal(192, chart.PeriodSlotTimes!.Count);
		});
	}

	[Fact]
	public void A_period_reaching_past_the_loaded_bars_reloads_and_keeps_the_older_bars()
	{
		var chart = TestUiThread.Invoke(CreateChart);
		WaitFor(() => chart.Bars.Count == 2);

		TestUiThread.Invoke(() => chart.SetPeriod(ChartPeriod.OneMonth));

		WaitFor(() => _ibkr.HistoricalCalls == 2 && !chart.IsLoading);
		TestUiThread.Invoke(() =>
		{
			Assert.Equal("32 D", _ibkr.HistoricalDurations[^1]);
			Assert.Equal(Now.AddMonths(-1), chart.PeriodStart);
			// Not cut to the period: the bar three days back is still there
			Assert.Equal(2, chart.Bars.Count);
		});
	}

	[Fact]
	public void Scrolling_to_the_oldest_bar_loads_the_page_before_it_until_ib_has_no_more()
	{
		var page = new TaskCompletionSource<IReadOnlyList<BarDto>>();
		_ibkr.HistoricalBefore = _ => page.Task;
		var chart = ConnectedChart();
		var oldest = Now.AddDays(-3);

		TestUiThread.Invoke(() =>
		{
			chart.LoadOlderBars();
			// One page at a time
			chart.LoadOlderBars();
		});
		WaitFor(() => OlderEnds().Count == 1);
		Thread.Sleep(50);
		Assert.Equal([oldest], OlderEnds());

		page.SetResult([Bar(oldest.AddMinutes(-10)), Bar(oldest.AddMinutes(-5))]);
		WaitFor(() => chart.Bars.Count == 4);
		TestUiThread.Invoke(() =>
			Assert.Equal(
				[oldest.AddMinutes(-10), oldest.AddMinutes(-5), oldest, Now.AddMinutes(-30)],
				chart.Bars.Select(x => x.Time)
			)
		);

		// Start of history: IB has nothing older, and nothing more is asked
		_ibkr.HistoricalBefore = _ => Task.FromResult<IReadOnlyList<BarDto>>([]);
		TestUiThread.Invoke(chart.LoadOlderBars);
		WaitFor(() => OlderEnds().Count == 2);
		TestUiThread.Invoke(() =>
		{
			Dispatcher.UIThread.RunJobs();
			chart.LoadOlderBars();
		});
		Thread.Sleep(50);
		Assert.Equal([oldest, oldest.AddMinutes(-10)], OlderEnds());
		Assert.Equal(4, TestUiThread.Invoke(() => chart.Bars.Count));
	}

	[Fact]
	public void A_page_still_on_its_way_never_lands_on_another_timeframe()
	{
		var page = new TaskCompletionSource<IReadOnlyList<BarDto>>();
		_ibkr.HistoricalBefore = _ => page.Task;
		var chart = ConnectedChart();
		var oldest = Now.AddDays(-3);
		TestUiThread.Invoke(chart.LoadOlderBars);

		_ibkr.HistoricalBy = (_, _) =>
			Task.FromResult<IReadOnlyList<BarDto>>([Bar(Now.AddHours(-1))]);
		TestUiThread.Invoke(() => chart.SetTimeframe(ChartTimeframe.OneHour));
		WaitFor(() => chart.Bars.Count == 1);

		page.SetResult([Bar(oldest.AddMinutes(-5))]);
		Thread.Sleep(50);
		TestUiThread.Invoke(() =>
		{
			Dispatcher.UIThread.RunJobs();
			Assert.Equal(Now.AddHours(-1), Assert.Single(chart.Bars).Time);
		});
	}

	[Fact]
	public void A_failed_page_is_logged_and_retried_only_after_a_while()
	{
		_ibkr.HistoricalBefore = _ =>
			Task.FromException<IReadOnlyList<BarDto>>(new InvalidOperationException("farm down"));
		var chart = ConnectedChart();

		TestUiThread.Invoke(chart.LoadOlderBars);
		WaitFor(() => _messages.GetAll().Any(x => x.Text.Contains("older bars unavailable")));
		TestUiThread.Invoke(() =>
		{
			Dispatcher.UIThread.RunJobs();
			chart.LoadOlderBars();
		});
		Thread.Sleep(50);
		Assert.Single(OlderEnds());

		_cache.Time.Advance(ChartConstants.OlderBarsRetryDelay);
		TestUiThread.Invoke(chart.LoadOlderBars);
		WaitFor(() => OlderEnds().Count == 2);
	}

	[Fact]
	public void No_page_is_asked_for_while_disconnected()
	{
		_ibkr.HistoricalBefore = _ => Task.FromResult<IReadOnlyList<BarDto>>([]);
		var chart = TestUiThread.Invoke(CreateChart);
		WaitFor(() => chart.Bars.Count == 2);

		TestUiThread.Invoke(chart.LoadOlderBars);
		Thread.Sleep(50);

		Assert.Empty(OlderEnds());
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

	private List<DateTimeOffset> OlderEnds() =>
		[.. _ibkr.HistoricalEnds.ToArray().OfType<DateTimeOffset>()];

	// Connected, the latest bars loaded and the live stream open
	private ChartViewModel ConnectedChart()
	{
		_ibkr.IsConnected = true;
		_ibkr.KeepRealtimeBarsOpen = true;
		_connection.SetStatus(ConnectionState.Connected, "DU1");
		var chart = TestUiThread.Invoke(CreateChart);
		WaitFor(() => chart.Bars.Count == 2 && !chart.IsLoading);
		return chart;
	}

	private static DateTimeOffset Utc(int month, int day, int hour, int minute = 0) =>
		new(2026, month, day, hour, minute, 0, TimeSpan.Zero);

	// SPY's hours around Friday 23 Oct 2026 (New York on EDT, UTC-4)
	private static ContractDetailsDto SpyDetails() =>
		new()
		{
			Contract = Spy,
			TimeZoneId = "US/Eastern",
			LiquidHours =
				"20261023:0930-20261023:1600;20261024:CLOSED;20261025:CLOSED;20261026:0930-20261026:1600",
			TradingHours =
				"20261023:0400-20261023:2000;20261024:CLOSED;20261025:CLOSED;20261026:0400-20261026:2000",
		};

	private static BarDto Bar(DateTimeOffset time) =>
		new()
		{
			Time = time,
			Open = 1,
			High = 1,
			Low = 1,
			Close = 1,
			Volume = 100,
		};

	private static void WaitFor(Func<bool> condition)
	{
		var until = DateTime.UtcNow.AddSeconds(5);
		while (
			!TestUiThread.Invoke(() =>
			{
				Dispatcher.UIThread.RunJobs();
				return condition();
			})
		)
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
			new FootprintManager(
				_ibkr,
				_cache.Manager,
				new MarketDataManager(_ibkr, TimeProvider.System),
				new InMemoryFootprintTapeRepository(),
				_cache.Time
			),
			new ContractManager(_ibkr, _cache.Settings, _cache.Manager),
			_connection,
			_messages,
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
			new OrderManager(_ibkr, _cache.Manager),
			new ChartDrawingClipboardStore(),
			new OpenOrdersStore(new OrderManager(_ibkr, _cache.Manager), _connection, _cache.Time),
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
