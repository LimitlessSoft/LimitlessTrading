using LSCore.Exceptions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketDepth;
using LT.Contracts.Enums;
using LT.Contracts.Exceptions;
using LT.Contracts.Requests.MarketDepth;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

public class MarketDepthManagerTests
{
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private static readonly ContractDto Aapl = new()
	{
		ConId = 265598,
		Symbol = "AAPL",
		SecurityType = "STK",
		Exchange = "SMART",
		PrimaryExchange = "NASDAQ",
		Currency = "USD",
	};

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public MarketDepthManagerTests() => LSCoreContainer.EnsureInitialized();

	private static SubscribeOrderBookRequest Request(int rows = 5) =>
		new() { Contract = Aapl, Rows = rows };

	private static MarketDepthUpdateDto Update(
		int position,
		MarketDepthOperation operation,
		MarketDepthSide side,
		double price,
		decimal size,
		string exchange
	) =>
		new()
		{
			Position = position,
			Operation = operation,
			Side = side,
			Price = price,
			Size = size,
			MarketMaker = exchange,
			IsSmartDepth = true,
		};

	[Fact]
	public async Task Builds_the_book_from_smart_depth_updates()
	{
		// As recorded on AAPL SMART depth (2026-10-26), with a delete past the end at the close
		_ibkr.DepthUpdates =
		[
			Update(0, MarketDepthOperation.Insert, MarketDepthSide.Bid, 343.84, 40, "IEX"),
			Update(1, MarketDepthOperation.Insert, MarketDepthSide.Bid, 343.80, 42, "IEX"),
			Update(0, MarketDepthOperation.Insert, MarketDepthSide.Ask, 344.05, 86, "IEX"),
			Update(0, MarketDepthOperation.Update, MarketDepthSide.Ask, 344.04, 40, "DRCTEDGE"),
			Update(0, MarketDepthOperation.Insert, MarketDepthSide.Bid, 343.99, 40, "MEMX"),
			Update(2, MarketDepthOperation.Delete, MarketDepthSide.Bid, 0, 0, "IEX"),
			Update(5, MarketDepthOperation.Delete, MarketDepthSide.Bid, 0, 0, "IEX"),
		];

		var books = await new MarketDepthManager(_ibkr)
			.SubscribeOrderBookAsync(Request(), Ct)
			.ToListAsync(Ct);

		Assert.Equal(6, books.Count);
		var book = books[^1];
		Assert.True(book.IsSmartDepth);
		Assert.Equal([343.99, 343.84], book.Bids.Select(x => x.Price));
		Assert.Equal(["MEMX", "IEX"], book.Bids.Select(x => x.MarketMaker));
		Assert.Equal(
			(344.04, 40m, "DRCTEDGE"),
			(book.Asks[0].Price, book.Asks[0].Size, book.Asks[0].MarketMaker)
		);
		Assert.Equal(2, books[1].Bids.Count);
	}

	[Fact]
	public async Task Asks_ib_for_smart_depth_by_default_and_the_listing_otherwise()
	{
		var manager = new MarketDepthManager(_ibkr);

		await manager.SubscribeOrderBookAsync(Request(20), Ct).ToListAsync(Ct);
		await manager
			.SubscribeOrderBookAsync(
				new SubscribeOrderBookRequest
				{
					Contract = Aapl,
					Rows = 10,
					PreferSmartDepth = false,
				},
				Ct
			)
			.ToListAsync(Ct);

		Assert.Equal(
			[("SMART", 20, true), ("NASDAQ", 10, false)],
			_ibkr.DepthRequests.Select(x => (x.Contract.Exchange, x.Rows, x.IsSmartDepth))
		);
	}

	[Fact]
	public async Task Refuses_more_than_the_depth_subscription_limit_and_frees_them_when_done()
	{
		_ibkr.KeepDepthOpen = true;
		var manager = new MarketDepthManager(_ibkr);
		var open = new List<CancellationTokenSource>();
		var reads = new List<Task>();
		for (var i = 0; i < MarketDepthConstants.MaxSubscriptions; i++)
		{
			var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
			open.Add(cts);
			var enumerator = manager
				.SubscribeOrderBookAsync(Request(), cts.Token)
				.GetAsyncEnumerator(cts.Token);
			reads.Add(enumerator.MoveNextAsync().AsTask());
		}
		while (_ibkr.DepthRequests.Count < MarketDepthConstants.MaxSubscriptions)
			await Task.Delay(10, Ct);

		var refused = await Assert.ThrowsAsync<LSCoreBadRequestException>(async () =>
			await manager.SubscribeOrderBookAsync(Request(), Ct).ToListAsync(Ct)
		);
		Assert.Contains("3 market depth", refused.Message);
		Assert.Equal(MarketDepthConstants.MaxSubscriptions, manager.ActiveSubscriptions);
		Assert.Equal(MarketDepthConstants.MaxSubscriptions, _ibkr.DepthRequests.Count);

		foreach (var cts in open)
			await cts.CancelAsync();
		foreach (var read in reads)
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
		Assert.Equal(0, manager.ActiveSubscriptions);
	}

	[Theory]
	[InlineData(309, "3 market depth")]
	[InlineData(354, "AAPL on SMART")]
	public async Task Ib_refusals_become_readable_and_free_the_subscription(
		int code,
		string expected
	)
	{
		_ibkr.DepthException = new IbkrRequestException(code, "refused");
		var manager = new MarketDepthManager(_ibkr);

		var error = await Assert.ThrowsAsync<LSCoreBadRequestException>(async () =>
			await manager.SubscribeOrderBookAsync(Request(), Ct).ToListAsync(Ct)
		);

		Assert.Contains(expected, error.Message);
		Assert.Equal(0, manager.ActiveSubscriptions);
	}

	[Fact]
	public async Task Other_ib_errors_pass_through()
	{
		_ibkr.DepthException = new IbkrRequestException(200, "No security definition");

		var error = await Assert.ThrowsAsync<IbkrRequestException>(async () =>
			await new MarketDepthManager(_ibkr)
				.SubscribeOrderBookAsync(Request(), Ct)
				.ToListAsync(Ct)
		);

		Assert.Equal(200, error.Code);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(MarketDepthConstants.MaxRows + 1)]
	public async Task Rows_must_be_within_the_limit(int rows)
	{
		await Assert.ThrowsAsync<LSCoreBadRequestException>(async () =>
			await new MarketDepthManager(_ibkr)
				.SubscribeOrderBookAsync(Request(rows), Ct)
				.ToListAsync(Ct)
		);
		Assert.Empty(_ibkr.DepthRequests);
	}

	[Fact]
	public async Task Throws_when_not_connected()
	{
		_ibkr.IsConnected = false;
		var manager = new MarketDepthManager(_ibkr);

		await Assert.ThrowsAsync<LSCoreBadRequestException>(async () =>
			await manager.SubscribeOrderBookAsync(Request(), Ct).ToListAsync(Ct)
		);
		Assert.Equal(0, manager.ActiveSubscriptions);
	}
}
