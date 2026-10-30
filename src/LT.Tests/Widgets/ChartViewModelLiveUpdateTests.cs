using Avalonia.Threading;
using LT.App.Stores;
using LT.App.Widgets.Chart;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Exceptions;
using LT.Contracts.Requests.Settings;
using LT.Domain.Managers;
using LT.Indicators.Scripting;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// Changing Chart live updates in Settings (app-wide, or a ticker's entry) reloads the open charts
/// it changes with the new mode.
/// </summary>
public class ChartViewModelLiveUpdateTests : IDisposable
{
	private static readonly ContractDto Spy = new()
	{
		ConId = 756733,
		Symbol = "SPY",
		SecurityType = "STK",
		Exchange = "SMART",
		Currency = "USD",
	};

	private static readonly ContractDto Aapl = new()
	{
		ConId = 265598,
		Symbol = "AAPL",
		SecurityType = "STK",
		Exchange = "SMART",
		Currency = "USD",
	};

	private static TickerSettingsDto Ticker(ContractDto contract, ChartLiveUpdateMode mode) =>
		new() { Contract = contract, ChartLiveUpdateMode = mode };

	private readonly TestCache _cache = new();
	private readonly FakeIbkrApiClient _ibkr = new();
	private readonly FakeConnectionManager _connection = new();
	private readonly MessageLogManager _messages = new();
	private readonly SettingsManager _settings;
	private readonly List<ChartViewModel> _charts = [];

	public ChartViewModelLiveUpdateTests()
	{
		_settings = new SettingsManager(_cache.Settings, _ibkr);
		// Every load asks IB, so a reload shows as a history request
		Save(ChartLiveUpdateMode.RealtimeBars, checkForUpdates: true);
	}

	[Fact]
	public void A_new_live_update_mode_reloads_open_charts() =>
		TestUiThread.Invoke(() =>
		{
			var chart = CreateChart();
			chart.OpenContract(Spy);
			Dispatcher.UIThread.RunJobs();
			var loads = _ibkr.HistoricalCalls;

			Save(ChartLiveUpdateMode.Quotes, checkForUpdates: true);
			Dispatcher.UIThread.RunJobs();

			Assert.Equal(loads + 1, _ibkr.HistoricalCalls);
		});

	[Fact]
	public void Other_settings_leave_charts_alone() =>
		TestUiThread.Invoke(() =>
		{
			var chart = CreateChart();
			chart.OpenContract(Spy);
			Dispatcher.UIThread.RunJobs();
			var loads = _ibkr.HistoricalCalls;

			Save(ChartLiveUpdateMode.RealtimeBars, checkForUpdates: false);
			// The redraw interval matters only to the throttled mode
			Save(ChartLiveUpdateMode.RealtimeBars, checkForUpdates: false, redrawIntervalMs: 300);
			Dispatcher.UIThread.RunJobs();

			Assert.Equal(loads, _ibkr.HistoricalCalls);
		});

	private int Loads(ContractDto contract) =>
		_ibkr.HistoricalRequests.Count(x => x.Contract.ConId == contract.ConId);

	[Fact]
	public void A_ticker_setting_reloads_only_the_charts_of_that_ticker() =>
		TestUiThread.Invoke(() =>
		{
			var spy1 = CreateChart();
			var spy2 = CreateChart();
			var aapl = CreateChart();
			spy1.OpenContract(Spy);
			spy2.OpenContract(Spy);
			aapl.OpenContract(Aapl);
			Dispatcher.UIThread.RunJobs();
			var (spyLoads, aaplLoads) = (Loads(Spy), Loads(Aapl));

			Save(
				ChartLiveUpdateMode.RealtimeBars,
				checkForUpdates: true,
				tickers: [Ticker(Spy, ChartLiveUpdateMode.TickByTick)]
			);
			Dispatcher.UIThread.RunJobs();

			Assert.Equal((spyLoads + 2, aaplLoads), (Loads(Spy), Loads(Aapl)));
		});

	[Fact]
	public void A_new_app_wide_mode_leaves_charts_of_a_listed_ticker_alone() =>
		TestUiThread.Invoke(() =>
		{
			Save(
				ChartLiveUpdateMode.RealtimeBars,
				checkForUpdates: true,
				tickers: [Ticker(Spy, ChartLiveUpdateMode.TickByTick)]
			);
			var spy = CreateChart();
			var aapl = CreateChart();
			spy.OpenContract(Spy);
			aapl.OpenContract(Aapl);
			Dispatcher.UIThread.RunJobs();
			var (spyLoads, aaplLoads) = (Loads(Spy), Loads(Aapl));

			Save(
				ChartLiveUpdateMode.Quotes,
				checkForUpdates: true,
				tickers: [Ticker(Spy, ChartLiveUpdateMode.TickByTick)]
			);
			Dispatcher.UIThread.RunJobs();
			Assert.Equal((spyLoads, aaplLoads + 1), (Loads(Spy), Loads(Aapl)));

			// Removing the entry: SPY falls back to the app-wide mode, which differs
			Save(ChartLiveUpdateMode.Quotes, checkForUpdates: true);
			Dispatcher.UIThread.RunJobs();
			Assert.Equal((spyLoads + 1, aaplLoads + 1), (Loads(Spy), Loads(Aapl)));
		});

	[Fact]
	public void A_listed_ticker_streams_with_its_own_mode()
	{
		_ibkr.IsConnected = true;
		_ibkr.KeepRealtimeBarsOpen = true;
		_ibkr.KeepTickByTickOpen = true;
		_ibkr.KeepMarketDataOpen = true;
		_connection.SetStatus(ConnectionState.Connected, "DU1");
		Save(
			ChartLiveUpdateMode.Quotes,
			checkForUpdates: true,
			tickers: [Ticker(Spy, ChartLiveUpdateMode.TickByTick)]
		);
		TestUiThread.Invoke(() =>
		{
			CreateChart().OpenContract(Spy);
			CreateChart().OpenContract(Aapl);
		});

		var deadline = DateTime.UtcNow.AddSeconds(5);
		while (
			(_ibkr.TickByTickRequests.Count == 0 || _ibkr.MarketDataRequested.Count == 0)
			&& DateTime.UtcNow < deadline
		)
		{
			TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());
			Thread.Sleep(10);
		}

		Assert.Equal(Spy.ConId, Assert.Single(_ibkr.TickByTickRequests).Contract.ConId);
		Assert.Equal(Aapl.ConId, Assert.Single(_ibkr.MarketDataRequested).ConId);
	}

	[Fact]
	public void A_refused_tick_by_tick_subscription_falls_back_to_quotes_and_is_logged()
	{
		_ibkr.IsConnected = true;
		_ibkr.KeepRealtimeBarsOpen = true;
		_ibkr.TickByTickException = new IbkrRequestException(
			10190,
			"Max number of tick-by-tick requests has been reached."
		);
		_connection.SetStatus(ConnectionState.Connected, "DU1");
		Save(ChartLiveUpdateMode.TickByTick, checkForUpdates: true);
		TestUiThread.Invoke(() => CreateChart().OpenContract(Spy));

		var deadline = DateTime.UtcNow.AddSeconds(5);
		while (_ibkr.GenericTicksRequested.Count == 0 && DateTime.UtcNow < deadline)
		{
			TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());
			Thread.Sleep(10);
		}

		Assert.Single(_ibkr.TickByTickRequests);
		Assert.Single(_ibkr.GenericTicksRequested);
		Assert.Contains(
			_messages.GetAll(),
			x => x.Text.Contains("tick-by-tick unavailable, using quotes")
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

	private void Save(
		ChartLiveUpdateMode mode,
		bool checkForUpdates,
		int redrawIntervalMs = 1000,
		List<TickerSettingsDto>? tickers = null
	) =>
		_settings.SaveAppSettings(
			new SaveAppSettingsRequest
			{
				MarketDataType = MarketDataType.Live,
				HeartbeatSeconds = 10,
				CheckForUpdates = checkForUpdates,
				CacheEnabled = false,
				ChartLiveUpdateMode = mode,
				ChartRedrawIntervalMs = redrawIntervalMs,
				TickerSettings = tickers ?? [],
			}
		);

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
		return chart;
	}
}
