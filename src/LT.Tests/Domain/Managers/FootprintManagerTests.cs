using LSCore.Exceptions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;
using LT.Contracts.Exceptions;
using LT.Contracts.Requests.Charts;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

/// <summary>Past trade pages, the tick size and live trades with their side for footprints.</summary>
public class FootprintManagerTests
{
	private static readonly ContractDto Aapl = new()
	{
		ConId = 265598,
		Symbol = "AAPL",
		SecurityType = "STK",
		Exchange = "SMART",
		Currency = "USD",
	};

	private static readonly DateTimeOffset T0 = new(2026, 10, 28, 14, 53, 26, TimeSpan.Zero);

	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private readonly TestCache _cache = new();
	private readonly InMemoryFootprintTapeRepository _tape = new();
	private readonly MarketDataManager _marketData;
	private readonly FootprintManager _manager;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public FootprintManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_marketData = new MarketDataManager(_ibkr, TimeProvider.System);
		_manager = new FootprintManager(_ibkr, _cache.Manager, _marketData, _tape, _cache.Time);
	}

	private static TickByTickDto Trade(double price, int second = 0) =>
		new()
		{
			Type = TickByTickType.AllLast,
			Time = T0.AddSeconds(second),
			Price = price,
			Size = 100,
		};

	// Outside regular hours too, so the cache holds every trade for any chart
	[Fact]
	public async Task A_page_asks_ib_for_a_thousand_trades_before_the_time()
	{
		_ibkr.HistoricalTicksBefore = _ => [Trade(10, -5)];

		var page = await _manager.GetTradesAsync(
			new GetFootprintTradesRequest { Contract = Aapl, Before = T0 },
			Ct
		);

		Assert.Single(page);
		var request = Assert.Single(_ibkr.HistoricalTickRequests);
		Assert.Equal(
			(HistoricalTickType.Trades, FootprintConstants.MaxTicksPerRequest, T0, false),
			(request.Type, request.Count, request.Before, request.UseRegularTradingHours)
		);
	}

	[Fact]
	public async Task A_page_is_recorded_from_its_oldest_trade_to_where_it_ends()
	{
		_cache.Time.SetUtcNow(T0.AddMinutes(1));
		_ibkr.HistoricalTicksBefore = _ => [Trade(10, -60), Trade(10.01, -5)];

		await _manager.GetTradesAsync(
			new GetFootprintTradesRequest { Contract = Aapl, Before = T0 },
			Ct
		);

		var range = Assert.Single(
			await _manager.GetRecordedTradesAsync(Aapl, T0.AddDays(-1), T0.AddDays(1), Ct)
		);
		Assert.Equal((T0.AddSeconds(-60), T0), (range.From, range.To));
		Assert.Equal([10, 10.01], range.Trades.Select(x => x.Price));
		Assert.All(range.Trades, x => Assert.Equal(FootprintSide.Unknown, x.QuoteSide));
	}

	// IB's history may still lack the last moment's trades: that part is paged again next time
	[Fact]
	public async Task A_page_ending_now_is_recorded_only_up_to_a_little_before()
	{
		_cache.Time.SetUtcNow(T0);
		_ibkr.HistoricalTicksBefore = _ => [Trade(10, -60), Trade(10.01, -5)];

		await _manager.GetTradesAsync(
			new GetFootprintTradesRequest { Contract = Aapl, Before = T0 },
			Ct
		);

		var range = Assert.Single(
			await _manager.GetRecordedTradesAsync(Aapl, T0.AddDays(-1), T0.AddDays(1), Ct)
		);
		Assert.Equal(
			(T0.AddSeconds(-60), T0 - FootprintConstants.TapeHistoricalSettle),
			(range.From, range.To)
		);
		Assert.Equal([10], range.Trades.Select(x => x.Price));
	}

	[Fact]
	public async Task An_empty_page_records_nothing()
	{
		_ibkr.HistoricalTicksBefore = _ => [];

		await _manager.GetTradesAsync(
			new GetFootprintTradesRequest { Contract = Aapl, Before = T0 },
			Ct
		);

		Assert.Equal(0, _tape.Appends);
	}

	// From the second after the first trade (its second may have started before the stream) to
	// the last one's second, with the quote rule's sides
	[Fact]
	public async Task Live_trades_are_recorded_in_whole_seconds_from_the_one_after_the_first()
	{
		_ibkr.TickByTicksByType[TickByTickType.AllLast] =
		[
			Trade(10),
			Trade(10.01, 1),
			Trade(10.02, 1),
			Trade(10.01, 4),
			Trade(10.03, 9),
		];

		await _manager.SubscribeTradesAsync(Aapl, cancellationToken: Ct).ToListAsync(Ct);

		var ranges = await _manager.GetRecordedTradesAsync(Aapl, T0.AddDays(-1), T0.AddDays(1), Ct);
		var range = Assert.Single(ranges);
		Assert.Equal((T0.AddSeconds(1), T0.AddSeconds(9)), (range.From, range.To));
		Assert.Equal([10.01, 10.02, 10.01], range.Trades.Select(x => x.Price));
		// Batches of at least 2 s: 01–04, then 04–09
		Assert.Equal(2, _tape.Appends);
	}

	[Fact]
	public async Task A_contract_without_a_conid_is_refused_before_asking_ib()
	{
		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.GetTradesAsync(
				new GetFootprintTradesRequest { Contract = new ContractDto(), Before = T0 },
				Ct
			)
		);
		Assert.Empty(_ibkr.HistoricalTickRequests);
	}

	[Fact]
	public async Task The_tick_size_is_the_contract_minimum_tick_or_a_cent()
	{
		_ibkr.SearchResults = [new ContractDetailsDto { Contract = Aapl, MinTick = 0.25 }];
		Assert.Equal(0.25, await _manager.GetTickSizeAsync(Aapl, Ct));

		var other = new ContractDto { ConId = 1, Symbol = "X" };
		_ibkr.SearchResults = [];
		Assert.Equal(
			FootprintConstants.FallbackTickSize,
			await _manager.GetTickSizeAsync(other, Ct)
		);
	}

	[Fact]
	public async Task Live_trades_take_all_trades_and_bid_ask_and_free_both_subscriptions()
	{
		_ibkr.TickByTicksByType[TickByTickType.AllLast] = [Trade(10), Trade(10.01, 1)];

		var trades = await _manager
			.SubscribeTradesAsync(Aapl, cancellationToken: Ct)
			.ToListAsync(Ct);

		Assert.Equal(2, trades.Count);
		Assert.Equal(TickByTickType.AllLast, _ibkr.TickByTickRequests[0].Type);
		Assert.Contains(_ibkr.TickByTickRequests, x => x.Type == TickByTickType.BidAsk);
		Assert.Equal(0, _marketData.ActiveTickByTick);
	}

	[Fact]
	public async Task Without_bid_ask_the_sides_go_by_the_tick_rule_and_the_caller_is_told()
	{
		_ibkr.TickByTicksByType[TickByTickType.AllLast] =
		[
			Trade(10),
			Trade(10.01, 1),
			Trade(10, 2),
		];
		_ibkr.TickByTicksByType[TickByTickType.BidAsk] = [];
		_ibkr.TickByTickExceptionByType[TickByTickType.BidAsk] = new IbkrRequestException(
			10190,
			"Max number of tick-by-tick requests has been reached."
		);
		_ibkr.KeepTickByTickOpen = true;
		Exception? quotesError = null;

		var trades = new List<FootprintTradeDto>();
		await foreach (var trade in _manager.SubscribeTradesAsync(Aapl, ex => quotesError = ex, Ct))
		{
			trades.Add(trade);
			if (trades.Count < 3)
				continue;
			var deadline = DateTime.UtcNow.AddSeconds(5);
			while (quotesError == null && DateTime.UtcNow < deadline)
				await Task.Delay(10, Ct);
			break;
		}

		Assert.Equal(
			[FootprintSide.Unknown, FootprintSide.Ask, FootprintSide.Bid],
			trades.Select(x => x.Side)
		);
		Assert.All(trades, x => Assert.True(x.ByTickRule));
		Assert.IsType<IbkrRequestException>(quotesError);
	}

	[Fact]
	public async Task A_refused_trade_subscription_fails_the_stream_without_asking_for_quotes()
	{
		_ibkr.TickByTicksByType[TickByTickType.AllLast] = [];
		_ibkr.TickByTickExceptionByType[TickByTickType.AllLast] = new IbkrRequestException(
			10190,
			"Max number of tick-by-tick requests has been reached."
		);

		var error = await Assert.ThrowsAsync<IbkrRequestException>(async () =>
			await _manager.SubscribeTradesAsync(Aapl, cancellationToken: Ct).ToListAsync(Ct)
		);

		Assert.Equal(10190, error.Code);
		Assert.DoesNotContain(_ibkr.TickByTickRequests, x => x.Type == TickByTickType.BidAsk);
		Assert.Equal(0, _marketData.ActiveTickByTick);
	}
}
