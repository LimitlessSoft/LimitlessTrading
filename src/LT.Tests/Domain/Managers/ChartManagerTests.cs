using LSCore.Exceptions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;
using LT.Contracts.Exceptions;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.Charts;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.Managers;

public class ChartManagerTests
{
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private readonly InMemoryCacheRepository _cache = new();
	private readonly InMemorySettingsRepository _settings = new();
	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 23, 15, 0, 0, TimeSpan.Zero)
	);
	private readonly MarketDataManager _marketData;
	private readonly ChartManager _manager;

	private static readonly GetBarsRequest Request = new()
	{
		Contract = new ContractDto
		{
			ConId = 265598,
			Symbol = "AAPL",
			SecurityType = "STK",
		},
		Timeframe = ChartTimeframe.FiveMinutes,
	};

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public ChartManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new ChartManager(
			_ibkr,
			new CacheManager(_cache, _settings, _time),
			_marketData = new MarketDataManager(_ibkr, TimeProvider.System),
			_time
		);
		_ibkr.HistoricalBars =
		[
			new BarDto
			{
				Time = _time.GetUtcNow(),
				Open = 1,
				High = 2,
				Low = 0.5,
				Close = 1.5,
			},
		];
	}

	[Fact]
	public async Task Fetches_and_caches_bars()
	{
		var result = await _manager.GetBarsAsync(Request, Ct);

		Assert.Single(result.Bars);
		Assert.False(result.IsStale);
		Assert.Single(_cache.Entries);
	}

	[Fact]
	public async Task Disabled_bar_cache_is_bypassed()
	{
		_settings.Settings.General.CacheChartBars = false;

		await _manager.GetBarsAsync(Request, Ct);
		await _manager.GetBarsAsync(Request, Ct);

		Assert.Equal(2, _ibkr.HistoricalCalls);
		Assert.Empty(_cache.Entries);
	}

	[Fact]
	public async Task Master_switch_off_bypasses_the_bar_cache()
	{
		_settings.Settings.General.CacheEnabled = false;

		await _manager.GetBarsAsync(Request, Ct);
		await _manager.GetBarsAsync(Request, Ct);

		Assert.Equal(2, _ibkr.HistoricalCalls);
	}

	[Fact]
	public async Task Serves_fresh_cache_without_asking_ib()
	{
		await _manager.GetBarsAsync(Request, Ct);
		_time.Advance(TimeSpan.FromSeconds(30));

		await _manager.GetBarsAsync(Request, Ct);

		Assert.Equal(1, _ibkr.HistoricalCalls);
	}

	[Fact]
	public async Task Refetches_after_the_cache_expires()
	{
		await _manager.GetBarsAsync(Request, Ct);
		_time.Advance(TimeSpan.FromMinutes(2));

		await _manager.GetBarsAsync(Request, Ct);

		Assert.Equal(2, _ibkr.HistoricalCalls);
	}

	[Fact]
	public async Task Falls_back_to_stale_cache_when_ib_fails()
	{
		await _manager.GetBarsAsync(Request, Ct);
		_time.Advance(TimeSpan.FromMinutes(2));
		_ibkr.HistoricalException = new InvalidOperationException("Not connected");

		var result = await _manager.GetBarsAsync(Request, Ct);

		Assert.True(result.IsStale);
		Assert.Equal("Not connected", result.Error);
		Assert.Single(result.Bars);
	}

	[Fact]
	public async Task Without_cache_the_error_propagates()
	{
		_ibkr.HistoricalException = new InvalidOperationException("Not connected");

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			_manager.GetBarsAsync(Request, Ct)
		);
	}

	[Fact]
	public async Task Unresolved_contract_is_rejected()
	{
		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.GetBarsAsync(
				new GetBarsRequest { Contract = new ContractDto { Symbol = "AAPL" } },
				Ct
			)
		);
	}

	[Fact]
	public async Task Auto_period_asks_for_the_timeframe_duration()
	{
		await _manager.GetBarsAsync(Request, Ct);

		Assert.Equal(["5 D"], _ibkr.HistoricalDurations);
	}

	[Fact]
	public async Task A_period_keeps_the_bars_before_its_start()
	{
		var now = _time.GetUtcNow();
		_ibkr.HistoricalBars =
		[
			new BarDto { Time = now.AddDays(-3) },
			new BarDto { Time = now.AddHours(-2) },
			new BarDto { Time = now },
		];

		var result = await _manager.GetBarsAsync(WithPeriod(ChartPeriod.Today), Ct);

		// Today is shorter than 5m's own 5 days: those are loaded, the view starts at midnight
		Assert.Equal(["5 D"], _ibkr.HistoricalDurations);
		Assert.Equal([now.AddDays(-3), now.AddHours(-2), now], result.Bars.Select(x => x.Time));
		Assert.Equal(new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero), result.From);
		Assert.Null(result.LimitedTo);
	}

	[Fact]
	public async Task A_period_longer_than_the_timeframes_span_asks_for_its_own()
	{
		var result = await _manager.GetBarsAsync(WithPeriod(ChartPeriod.SevenDays), Ct);

		Assert.Equal(["8 D"], _ibkr.HistoricalDurations);
		Assert.Equal(_time.GetUtcNow().AddDays(-7), result.From);
	}

	[Fact]
	public async Task Too_long_a_period_is_cut_and_reported()
	{
		var result = await _manager.GetBarsAsync(WithPeriod(ChartPeriod.OneYear), Ct);

		Assert.Equal(ChartPeriod.OneMonth, result.LimitedTo);
		Assert.Equal(_time.GetUtcNow().AddMonths(-1), result.From);
		Assert.Equal(["32 D"], _ibkr.HistoricalDurations);
	}

	[Fact]
	public async Task Periods_with_other_durations_are_cached_apart()
	{
		await _manager.GetBarsAsync(Request, Ct);
		await _manager.GetBarsAsync(WithPeriod(ChartPeriod.Today), Ct);
		await _manager.GetBarsAsync(WithPeriod(ChartPeriod.SevenDays), Ct);

		// Today loads Auto's "5 D" (from the cache); 7 days is "8 D"
		Assert.Equal(2, _ibkr.HistoricalCalls);
		Assert.Equal(2, _cache.Entries.Count);
	}

	[Fact]
	public async Task An_older_page_ends_at_the_oldest_bar_and_holds_only_older_bars()
	{
		var before = _time.GetUtcNow().AddDays(-5);
		_ibkr.HistoricalBefore = end =>
			Task.FromResult<IReadOnlyList<BarDto>>([
				new BarDto { Time = end.AddMinutes(-10), Close = 1 },
				new BarDto { Time = end.AddMinutes(-5), Close = 2 },
				new BarDto { Time = end, Close = 3 }, // IB's end is inclusive
			]);

		var bars = await _manager.GetOlderBarsAsync(Older(before), Ct);

		Assert.Equal([before.AddMinutes(-10), before.AddMinutes(-5)], bars.Select(x => x.Time));
		Assert.Equal([before], _ibkr.HistoricalEnds.Cast<DateTimeOffset>());
		Assert.Equal(["1 M"], _ibkr.HistoricalDurations);
		Assert.Equal(["5 mins"], _ibkr.HistoricalBarSizes);
	}

	[Fact]
	public async Task Older_pages_are_cached_by_where_they_end()
	{
		var before = _time.GetUtcNow().AddDays(-5);
		_ibkr.HistoricalBefore = end =>
			Task.FromResult<IReadOnlyList<BarDto>>([new BarDto { Time = end.AddMinutes(-5) }]);

		await _manager.GetOlderBarsAsync(Older(before), Ct);
		await _manager.GetOlderBarsAsync(Older(before), Ct);
		await _manager.GetOlderBarsAsync(Older(before.AddMinutes(-5)), Ct);

		Assert.Equal(2, _ibkr.HistoricalCalls);
	}

	[Fact]
	public async Task Ibs_no_data_before_the_first_bar_is_an_empty_page_not_cached()
	{
		_ibkr.HistoricalBefore = _ =>
			Task.FromException<IReadOnlyList<BarDto>>(
				new IbkrRequestException(
					ChartConstants.HistoricalDataErrorCode,
					"[162] Historical Market Data Service error message:HMDS query returned no data: AAPL@SMART Trades (reqId 7)"
				)
			);
		var before = _time.GetUtcNow().AddYears(-30);

		Assert.Empty(await _manager.GetOlderBarsAsync(Older(before), Ct));
		Assert.Empty(await _manager.GetOlderBarsAsync(Older(before), Ct));
		Assert.Equal(2, _ibkr.HistoricalCalls);
	}

	[Fact]
	public async Task Other_ib_errors_of_an_older_page_propagate()
	{
		_ibkr.HistoricalBefore = _ =>
			Task.FromException<IReadOnlyList<BarDto>>(
				new IbkrRequestException(
					ChartConstants.HistoricalDataErrorCode,
					"[162] Historical Market Data Service error message:API historical data query cancelled"
				)
			);

		await Assert.ThrowsAsync<IbkrRequestException>(() =>
			_manager.GetOlderBarsAsync(Older(_time.GetUtcNow().AddDays(-5)), Ct)
		);
	}

	[Fact]
	public async Task Older_bars_of_an_unresolved_contract_are_rejected()
	{
		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.GetOlderBarsAsync(new GetOlderBarsRequest { Before = _time.GetUtcNow() }, Ct)
		);
		Assert.Equal(0, _ibkr.HistoricalCalls);
	}

	private static GetOlderBarsRequest Older(DateTimeOffset before) =>
		new()
		{
			Contract = Request.Contract,
			Timeframe = Request.Timeframe,
			Before = before,
		};

	private static GetBarsRequest WithPeriod(ChartPeriod period) =>
		new()
		{
			Contract = Request.Contract,
			Timeframe = Request.Timeframe,
			Period = period,
		};

	[Fact]
	public async Task Quote_modes_stream_one_price_per_trade_on_a_market_data_line()
	{
		_ibkr.Ticks =
		[
			new MarketDataTickDto { Field = TickTypeHelpers.Bid, Price = 99.9 },
			new MarketDataTickDto { Field = TickTypeHelpers.Last, Price = 100 },
			new MarketDataTickDto { Field = TickTypeHelpers.Ask, Price = 100.1 },
			new MarketDataTickDto { Field = TickTypeHelpers.Last, Price = 100.2 },
			new MarketDataTickDto { Field = TickTypeHelpers.LastSize, Size = 3 },
		];
		var linesInUse = new List<int>();

		var prices = new List<ChartPriceDto>();
		await foreach (
			var price in _manager.SubscribeLivePricesAsync(
				Request,
				ChartLiveUpdateMode.QuotesThrottled,
				Ct
			)
		)
		{
			prices.Add(price);
			linesInUse.Add(_marketData.ActiveLines);
		}

		Assert.Equal([100d, 100.2, 100.2], prices.Select(x => x.Price));
		Assert.All(prices, x => Assert.Equal(_time.GetUtcNow(), x.Time));
		Assert.All(linesInUse, x => Assert.Equal(1, x));
		Assert.Equal(0, _marketData.ActiveLines);
	}

	[Fact]
	public async Task Without_a_free_market_data_line_the_price_stream_fails()
	{
		for (var i = 0; i < MarketDataConstants.MaxLines; i++)
			_marketData.AcquireLine(MarketDataStreamKind.News, new ContractDto());

		await Assert.ThrowsAsync<LSCoreBadRequestException>(async () =>
			await _manager
				.SubscribeLivePricesAsync(Request, ChartLiveUpdateMode.Quotes, Ct)
				.ToListAsync(Ct)
		);
	}

	[Fact]
	public async Task Five_second_bars_need_no_price_stream()
	{
		_ibkr.Ticks = [new MarketDataTickDto { Field = TickTypeHelpers.Last, Price = 100 }];

		Assert.Empty(
			await _manager
				.SubscribeLivePricesAsync(Request, ChartLiveUpdateMode.RealtimeBars, Ct)
				.ToListAsync(Ct)
		);
	}

	[Fact]
	public async Task Tick_by_tick_streams_trades_with_ibs_time_on_a_counted_subscription()
	{
		var ibTime = _time.GetUtcNow().AddSeconds(-2);
		_ibkr.TickByTicks =
		[
			new TickByTickDto
			{
				Type = TickByTickType.Last,
				Time = ibTime,
				Price = 100,
				Size = 5,
			},
			new TickByTickDto
			{
				Type = TickByTickType.Last,
				Time = ibTime,
				Price = 100.1,
				Size = 1,
			},
		];
		var inUse = new List<int>();

		var prices = new List<ChartPriceDto>();
		await foreach (
			var price in _manager.SubscribeLivePricesAsync(
				Request,
				ChartLiveUpdateMode.TickByTickThrottled,
				Ct
			)
		)
		{
			prices.Add(price);
			inUse.Add(_marketData.ActiveTickByTick);
		}

		Assert.Equal([(ibTime, 100d), (ibTime, 100.1)], prices.Select(x => (x.Time, x.Price)));
		Assert.Equal(TickByTickType.Last, Assert.Single(_ibkr.TickByTickRequests).Type);
		Assert.All(inUse, x => Assert.Equal(1, x));
		Assert.Equal(0, _marketData.ActiveTickByTick);
		Assert.Equal(0, _marketData.ActiveLines);
	}

	[Fact]
	public async Task Forex_tick_by_tick_asks_for_the_midpoint()
	{
		var eurUsd = new GetBarsRequest
		{
			Contract = new ContractDto
			{
				ConId = 12087792,
				Symbol = "EUR",
				SecurityType = MarketDataConstants.ForexType,
			},
			Timeframe = ChartTimeframe.OneMinute,
		};

		await _manager
			.SubscribeLivePricesAsync(eurUsd, ChartLiveUpdateMode.TickByTick, Ct)
			.ToListAsync(Ct);

		Assert.Equal(TickByTickType.MidPoint, Assert.Single(_ibkr.TickByTickRequests).Type);
	}

	[Fact]
	public async Task Ibs_tick_by_tick_refusal_ends_the_stream_with_its_code()
	{
		_ibkr.TickByTickException = new IbkrRequestException(
			10190,
			"Max number of tick-by-tick requests has been reached."
		);

		var ex = await Assert.ThrowsAsync<IbkrRequestException>(async () =>
			await _manager
				.SubscribeLivePricesAsync(Request, ChartLiveUpdateMode.TickByTick, Ct)
				.ToListAsync(Ct)
		);

		Assert.Equal(10190, ex.Code);
		Assert.Equal(0, _marketData.ActiveTickByTick);
	}

	[Fact]
	public async Task Regular_hours_come_from_the_contract_details_and_are_cached()
	{
		_ibkr.SearchResults =
		[
			new ContractDetailsDto
			{
				Contract = Request.Contract,
				TimeZoneId = "US/Eastern",
				TradingHours = "20261026:0400-20261026:2000",
				LiquidHours = "20261026:0930-20261026:1600",
			},
		];

		var schedule = await _manager.GetRegularHoursAsync(Request.Contract, Ct);
		await _manager.GetRegularHoursAsync(Request.Contract, Ct);

		var session = Assert.Single(
			TradingHoursHelpers.GetSessions(schedule!, new DateOnly(2026, 10, 26))
		);
		Assert.Equal(new TimeOnly(9, 30), session.Start);
		Assert.Equal(1, _ibkr.SearchCalls);
	}

	[Fact]
	public async Task A_contract_without_liquid_hours_has_no_regular_hours()
	{
		_ibkr.SearchResults = [new ContractDetailsDto { Contract = Request.Contract }];

		Assert.Null(await _manager.GetRegularHoursAsync(Request.Contract, Ct));
	}
}
