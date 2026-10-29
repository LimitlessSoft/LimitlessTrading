using Avalonia.Threading;
using LSCore.Exceptions;
using LT.App.Stores;
using LT.App.Widgets.Chart;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Requests.Settings;
using LT.Domain.Managers;
using LT.Indicators.Scripting;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// A chart switched to footprint bars backfills past trades and streams live ones; candles ask
/// for neither; the style is saved per chart; a refused live stream is said.
/// </summary>
public class ChartViewModelFootprintTests : IDisposable
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
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private readonly FakeConnectionManager _connection = new();
	private readonly MessageLogManager _messages = new();

	// Shared by the charts of a test: what one recorded, the next one reads
	private readonly InMemoryFootprintTapeRepository _tape = new();
	private readonly SettingsManager _settings;
	private readonly List<ChartViewModel> _charts = [];

	public ChartViewModelFootprintTests()
	{
		LSCoreContainer.EnsureInitialized();
		_settings = new SettingsManager(_cache.Settings, _ibkr);
		_settings.SaveAppSettings(
			new SaveAppSettingsRequest
			{
				MarketDataType = MarketDataType.Live,
				HeartbeatSeconds = 10,
				CacheEnabled = false,
				ChartFootprintImbalancePercent = 250,
			}
		);
		var now = _cache.Time.GetUtcNow();
		_ibkr.HistoricalBars =
		[
			new BarDto
			{
				Time = now.AddMinutes(-10),
				Open = 1,
				High = 2,
				Low = 0.5,
				Close = 1.5,
			},
			new BarDto
			{
				Time = now.AddMinutes(-5),
				Open = 1.5,
				High = 2,
				Low = 1,
				Close = 1.2,
			},
		];
		_ibkr.KeepTickByTickOpen = true;
		_connection.SetStatus(ConnectionState.Connected, "DU1");
	}

	[Fact]
	public void A_footprint_chart_backfills_past_trades_and_streams_live_ones()
	{
		var chart = TestUiThread.Invoke(() =>
		{
			var chart = CreateChart();
			chart.LoadState(
				new WidgetPlacementDto
				{
					ChartBarStyle = ChartBarStyle.Footprint,
					ChartContract = Spy,
					ChartTimeframe = ChartTimeframe.FiveMinutes,
				}
			);
			return chart;
		});

		WaitFor(() => _ibkr.HistoricalTickRequests.Count > 0 && _ibkr.TickByTickRequests.Count > 0);

		var request = _ibkr.HistoricalTickRequests[0];
		Assert.Equal(HistoricalTickType.Trades, request.Type);
		Assert.Equal(_cache.Time.GetUtcNow(), request.Before);
		Assert.Equal(TickByTickType.AllLast, _ibkr.TickByTickRequests[0].Type);
		TestUiThread.Invoke(() =>
		{
			Assert.NotNull(chart.Footprint);
			Assert.Equal(2.5, chart.FootprintImbalanceRatio);
			var saved = new WidgetPlacementDto();
			chart.SaveState(saved);
			Assert.Equal(ChartBarStyle.Footprint, saved.ChartBarStyle);
		});
	}

	// Bars older than the session (scrolled back to) loaded no footprint: the backfill had ended
	// at the session's first bar for good
	[Fact]
	public void Scrolling_back_past_the_session_loads_the_older_bars_footprints()
	{
		var now = _cache.Time.GetUtcNow();
		var yesterday = now.AddDays(-1).AddMinutes(-5);
		_ibkr.HistoricalBars =
		[
			new BarDto
			{
				Time = yesterday,
				Open = 1,
				High = 2,
				Low = 0.5,
				Close = 1.5,
			},
			.. _ibkr.HistoricalBars,
		];
		// One trade six minutes before each page's end: every page completes a bar
		_ibkr.HistoricalTicksBefore = before =>
			[
				new TickByTickDto
				{
					Type = TickByTickType.AllLast,
					Time = before.AddMinutes(-6),
					Price = 1,
					Size = 100,
				},
			];
		var chart = TestUiThread.Invoke(() =>
		{
			var chart = CreateChart();
			// The view shows today's bars
			chart.ShowFootprintBars(now.AddMinutes(-10), now.AddMinutes(-5));
			chart.LoadState(
				new WidgetPlacementDto
				{
					ChartBarStyle = ChartBarStyle.Footprint,
					ChartContract = Spy,
					ChartTimeframe = ChartTimeframe.FiveMinutes,
				}
			);
			return chart;
		});
		WaitFor(() =>
			TestUiThread.Invoke(() =>
				chart.Footprint?.GetState(now.AddMinutes(-10)) == FootprintBarState.Ready
			)
		);
		var sessionRequests = _ibkr.HistoricalTickRequests.Count;
		TestUiThread.Invoke(() =>
			Assert.Equal(FootprintBarState.Loading, chart.Footprint!.GetState(yesterday))
		);

		TestUiThread.Invoke(() => chart.ShowFootprintBars(yesterday, now.AddMinutes(-5)));
		WaitFor(() =>
			TestUiThread.Invoke(() =>
				chart.Footprint?.GetState(yesterday) == FootprintBarState.Ready
			)
		);

		Assert.Contains(
			_ibkr.HistoricalTickRequests.Skip(sessionRequests),
			x => x.Before == yesterday.AddMinutes(5)
		);
	}

	// Reopening a chart (or restarting the app): the bars complete before come from the cache at
	// once, and IB is asked only for the trades since
	[Fact]
	public void A_second_load_takes_the_cached_trades_and_pages_only_what_is_new()
	{
		var start = _cache.Time.GetUtcNow();
		// One trade six minutes before each page's end: every page completes a bar
		_ibkr.HistoricalTicksBefore = before =>
			[
				new TickByTickDto
				{
					Type = TickByTickType.AllLast,
					Time = before.AddMinutes(-6),
					Price = 1,
					Size = 100,
				},
			];
		var first = LoadFootprintChart();
		WaitFor(() =>
			TestUiThread.Invoke(() =>
				first.Footprint?.GetState(start.AddMinutes(-10)) == FootprintBarState.Ready
			)
		);
		Assert.Equal(
			[start, start.AddMinutes(-6)],
			_ibkr.HistoricalTickRequests.Select(x => x.Before)
		);
		TestUiThread.Invoke(() => first.Dispose());

		// Five minutes later, with a new bar
		_cache.Time.Advance(TimeSpan.FromMinutes(5));
		_ibkr.HistoricalBars =
		[
			.. _ibkr.HistoricalBars,
			new BarDto
			{
				Time = start,
				Open = 1.2,
				High = 1.3,
				Low = 1.1,
				Close = 1.25,
			},
		];
		_ibkr.HistoricalTickRequests.Clear();
		var second = LoadFootprintChart();
		WaitFor(() =>
			TestUiThread.Invoke(() => second.Footprint?.GetState(start) == FootprintBarState.Ready)
		);

		TestUiThread.Invoke(() =>
		{
			Assert.Equal(
				FootprintBarState.Ready,
				second.Footprint!.GetState(start.AddMinutes(-10))
			);
			Assert.Equal(FootprintBarState.Ready, second.Footprint.GetState(start.AddMinutes(-5)));
		});
		var request = Assert.Single(_ibkr.HistoricalTickRequests);
		Assert.Equal(start.AddMinutes(5), request.Before);
	}

	private ChartViewModel LoadFootprintChart() =>
		TestUiThread.Invoke(() =>
		{
			var chart = CreateChart();
			chart.LoadState(
				new WidgetPlacementDto
				{
					ChartBarStyle = ChartBarStyle.Footprint,
					ChartContract = Spy,
					ChartTimeframe = ChartTimeframe.FiveMinutes,
				}
			);
			return chart;
		});

	[Fact]
	public void A_candle_chart_asks_for_no_trades_and_saves_no_style()
	{
		var chart = TestUiThread.Invoke(() =>
		{
			var chart = CreateChart();
			chart.OpenContract(Spy);
			return chart;
		});
		WaitFor(() => _ibkr.HistoricalCalls > 0);
		TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());

		Assert.Empty(_ibkr.HistoricalTickRequests);
		Assert.DoesNotContain(_ibkr.TickByTickRequests, x => x.Type == TickByTickType.AllLast);
		TestUiThread.Invoke(() =>
		{
			Assert.Null(chart.Footprint);
			var saved = new WidgetPlacementDto();
			chart.SaveState(saved);
			Assert.Null(saved.ChartBarStyle);
		});
	}

	[Fact]
	public void Switching_back_to_candles_drops_the_footprint()
	{
		var chart = TestUiThread.Invoke(() =>
		{
			var chart = CreateChart();
			chart.OpenContract(Spy);
			return chart;
		});
		WaitFor(() => _ibkr.HistoricalCalls > 0);

		TestUiThread.Invoke(() => chart.ToggleBarStyle());
		WaitFor(() => TestUiThread.Invoke(() => chart.Footprint != null));
		TestUiThread.Invoke(() => chart.ToggleBarStyle());

		TestUiThread.Invoke(() => Assert.Null(chart.Footprint));
	}

	[Fact]
	public void The_tick_by_tick_limit_is_said_in_the_status_line_and_the_log()
	{
		_ibkr.TickByTickExceptionByType[TickByTickType.AllLast] = new LSCoreBadRequestException(
			$"Tick-by-tick subscription limit reached ({MarketDataConstants.MaxTickByTick})."
		);
		var chart = TestUiThread.Invoke(() =>
		{
			var chart = CreateChart();
			chart.LoadState(
				new WidgetPlacementDto
				{
					ChartBarStyle = ChartBarStyle.Footprint,
					ChartContract = Spy,
					ChartTimeframe = ChartTimeframe.FiveMinutes,
				}
			);
			return chart;
		});

		WaitFor(() => TestUiThread.Invoke(() => chart.StatusText?.Contains("not live") == true));

		Assert.Contains(
			_messages.GetAll(),
			x => x.Text.Contains("footprint live trades unavailable") && x.Text.Contains("limit")
		);
	}

	// Runs the UI thread's jobs until the condition holds (5 s at most)
	private static void WaitFor(Func<bool> condition)
	{
		var deadline = DateTime.UtcNow.AddSeconds(5);
		while (!condition() && DateTime.UtcNow < deadline)
		{
			TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());
			Thread.Sleep(10);
		}
		TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());
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
				_tape,
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
