using System.Threading.Channels;
using LSCore.Exceptions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;
using LT.Contracts.Exceptions;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

/// <summary>
/// Tick-by-tick subscriptions of one contract and tick type share one IB subscription, which is
/// what counts against IB's limit of 5: the first opens it, later ones join it, the last one to
/// leave cancels it; an IB error ends all of them.
/// </summary>
public class MarketDataManagerTickByTickSharingTests
{
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private static readonly ContractDto Spy = new()
	{
		ConId = 756733,
		Symbol = "SPY",
		SecurityType = "STK",
		Exchange = "SMART",
	};

	private readonly FakeIbkrApiClient _ibkr = new()
	{
		IsConnected = true,
		TickByTickStreamGate = Channel.CreateUnbounded<TickByTickDto>(),
	};

	private static TickByTickDto Trade(double price) =>
		new()
		{
			Type = TickByTickType.Last,
			Time = DateTimeOffset.UnixEpoch,
			Price = price,
			Size = 1,
		};

	// One subscription read on its own task, its prices collected
	private sealed class Reader
	{
		public Task Running { get; set; } = Task.CompletedTask;
		public CancellationTokenSource Stop { get; } =
			CancellationTokenSource.CreateLinkedTokenSource(Ct);
		private readonly List<double> _prices = [];

		public void Add(double price)
		{
			lock (_prices)
				_prices.Add(price);
		}

		public IReadOnlyList<double> Prices
		{
			get
			{
				lock (_prices)
					return [.. _prices];
			}
		}
	}

	private static Reader Read(
		MarketDataManager manager,
		ContractDto contract,
		TickByTickType tickType = TickByTickType.Last
	)
	{
		var reader = new Reader();
		reader.Running = Task.Run(
			async () =>
			{
				await foreach (
					var tick in manager.SubscribeTickByTickAsync(
						contract,
						tickType,
						reader.Stop.Token
					)
				)
					reader.Add(tick.Price);
			},
			Ct
		);
		return reader;
	}

	private static async Task WaitUntil(Func<bool> condition)
	{
		var deadline = DateTime.UtcNow.AddSeconds(5);
		while (!condition())
		{
			if (DateTime.UtcNow > deadline)
				throw new TimeoutException("condition not met");
			await Task.Delay(5, Ct);
		}
	}

	private static async Task StopAll(params Reader[] readers)
	{
		foreach (var reader in readers)
			await reader.Stop.CancelAsync();
		foreach (var reader in readers)
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.Running);
	}

	[Fact]
	public async Task Two_charts_of_one_ticker_share_one_ib_subscription_and_one_slot()
	{
		var manager = new MarketDataManager(_ibkr, TimeProvider.System);
		var first = Read(manager, Spy);
		await WaitUntil(() => _ibkr.OpenTickByTickStreams == 1);
		// The same contract from another widget: an equal copy, not the same object
		var second = Read(
			manager,
			new ContractDto
			{
				ConId = Spy.ConId,
				Symbol = "SPY",
				Exchange = "SMART",
			}
		);
		await WaitUntil(() => manager.ActiveTickByTick == 1);
		// Joined before the next tick: both get it
		await Task.Delay(50, Ct);

		await _ibkr.TickByTickStreamGate!.Writer.WriteAsync(Trade(500), Ct);
		await WaitUntil(() => first.Prices.Count == 1 && second.Prices.Count == 1);

		Assert.Equal([500d], first.Prices);
		Assert.Equal([500d], second.Prices);
		Assert.Single(_ibkr.TickByTickRequests);
		Assert.Equal(1, manager.ActiveTickByTick);
		Assert.Equal(1, _ibkr.OpenTickByTickStreams);
		await StopAll(first, second);
	}

	[Fact]
	public async Task Cancelling_one_leaves_the_other_and_the_last_one_frees_the_slot()
	{
		var manager = new MarketDataManager(_ibkr, TimeProvider.System);
		var first = Read(manager, Spy);
		var second = Read(manager, Spy);
		await WaitUntil(() => _ibkr.OpenTickByTickStreams == 1);
		await Task.Delay(50, Ct);

		await StopAll(first);
		await _ibkr.TickByTickStreamGate!.Writer.WriteAsync(Trade(501), Ct);
		await WaitUntil(() => second.Prices.Count == 1);
		Assert.Equal(1, manager.ActiveTickByTick);
		Assert.Equal(1, _ibkr.OpenTickByTickStreams);

		await StopAll(second);
		Assert.Equal(0, manager.ActiveTickByTick);
		await WaitUntil(() => _ibkr.OpenTickByTickStreams == 0);

		// The next one opens a fresh subscription
		var third = Read(manager, Spy);
		await WaitUntil(() => _ibkr.OpenTickByTickStreams == 1);
		Assert.Equal(2, _ibkr.TickByTickRequests.Count);
		await StopAll(third);
	}

	[Fact]
	public async Task Other_tick_types_and_contracts_take_their_own_slot_up_to_ibs_limit()
	{
		var manager = new MarketDataManager(_ibkr, TimeProvider.System);
		var readers = new List<Reader>
		{
			Read(manager, Spy, TickByTickType.Last),
			Read(manager, Spy, TickByTickType.AllLast),
			Read(manager, Spy, TickByTickType.BidAsk),
		};
		for (var conId = 1; readers.Count < MarketDataConstants.MaxTickByTick; conId++)
			readers.Add(Read(manager, new ContractDto { ConId = conId, Symbol = $"X{conId}" }));
		await WaitUntil(() => manager.ActiveTickByTick == MarketDataConstants.MaxTickByTick);

		// At the limit a new contract is refused before asking IB …
		await Assert.ThrowsAsync<LSCoreBadRequestException>(async () =>
			await manager
				.SubscribeTickByTickAsync(
					new ContractDto { ConId = 99, Symbol = "QQQ" },
					TickByTickType.Last,
					Ct
				)
				.ToListAsync(Ct)
		);
		// … but another chart of a ticker already streaming joins it
		var joined = Read(manager, Spy, TickByTickType.Last);
		await Task.Delay(50, Ct);
		Assert.False(joined.Running.IsCompleted);

		Assert.Equal(MarketDataConstants.MaxTickByTick, _ibkr.TickByTickRequests.Count);
		Assert.Equal(MarketDataConstants.MaxTickByTick, manager.ActiveTickByTick);
		await StopAll([.. readers, joined]);
		Assert.Equal(0, manager.ActiveTickByTick);
	}

	[Fact]
	public async Task An_ib_error_ends_every_subscriber_and_frees_the_slot()
	{
		var manager = new MarketDataManager(_ibkr, TimeProvider.System);
		var first = Read(manager, Spy);
		var second = Read(manager, Spy);
		await WaitUntil(() => _ibkr.OpenTickByTickStreams == 1);
		await Task.Delay(50, Ct);

		_ibkr.TickByTickStreamGate!.Writer.Complete(
			new IbkrRequestException(10189, "Failed to request tick-by-tick data.")
		);

		var a = await Assert.ThrowsAsync<IbkrRequestException>(() => first.Running);
		var b = await Assert.ThrowsAsync<IbkrRequestException>(() => second.Running);
		Assert.Equal((10189, 10189), (a.Code, b.Code));
		Assert.Equal(0, manager.ActiveTickByTick);
	}

	[Fact]
	public async Task Two_footprints_of_one_ticker_take_two_slots_not_four()
	{
		LSCoreContainer.EnsureInitialized();
		var cache = new TestCache();
		var manager = new MarketDataManager(_ibkr, TimeProvider.System);
		var footprints = new FootprintManager(
			_ibkr,
			cache.Manager,
			manager,
			new InMemoryFootprintTapeRepository(),
			cache.Time
		);
		// Trades when written, quotes open and silent
		var trades = Channel.CreateUnbounded<TickByTickDto>();
		_ibkr.TickByTickStreamGate = null;
		_ibkr.TickByTickStreamGatesByType[TickByTickType.AllLast] = trades;
		_ibkr.TickByTicksByType[TickByTickType.BidAsk] = [];
		_ibkr.KeepTickByTickOpen = true;
		using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
		var running = Enumerable
			.Range(0, 2)
			.Select(_ =>
				Task.Run(
					async () =>
					{
						await foreach (
							var _ in footprints.SubscribeTradesAsync(
								Spy,
								cancellationToken: stop.Token
							)
						) { }
					},
					Ct
				)
			)
			.ToList();
		await WaitUntil(() => _ibkr.OpenTickByTickStreams == 1);
		await Task.Delay(50, Ct);
		// A trade reaches both, so both ask for the quotes
		await trades.Writer.WriteAsync(
			new TickByTickDto
			{
				Type = TickByTickType.AllLast,
				Time = DateTimeOffset.UnixEpoch,
				Price = 500,
				Size = 1,
			},
			Ct
		);
		await WaitUntil(() => _ibkr.OpenTickByTickStreams == 2);
		await Task.Delay(100, Ct);

		Assert.Equal(
			[TickByTickType.AllLast, TickByTickType.BidAsk],
			_ibkr.TickByTickRequests.Select(x => x.Type)
		);
		Assert.Equal(2, manager.ActiveTickByTick);
		Assert.Equal(2, _ibkr.OpenTickByTickStreams);
		await stop.CancelAsync();
		foreach (var task in running)
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
		await WaitUntil(() => manager.ActiveTickByTick == 0);
	}
}
