using System.Threading.Channels;
using LSCore.Exceptions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;
using LT.Contracts.Exceptions;
using LT.Contracts.Helpers;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.Managers;

/// <summary>
/// Quote subscriptions of one contract share one IB line: the first opens it, later ones join
/// with the latest quote, the last one to leave closes it; an IB error ends all of them. And
/// snapshots, which take no line.
/// </summary>
public class MarketDataManagerSharingTests
{
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private static readonly ContractDto Aapl = new()
	{
		ConId = 265598,
		Symbol = "AAPL",
		Exchange = "SMART",
	};

	private readonly FakeIbkrApiClient _ibkr = new()
	{
		IsConnected = true,
		Ticks = [new MarketDataTickDto { Field = TickTypeHelpers.Last, Price = 100 }],
		MarketDataStreamGate = Channel.CreateUnbounded<MarketDataTickDto>(),
	};

	private static MarketDataTickDto Last(double price) =>
		new() { Field = TickTypeHelpers.Last, Price = price };

	// One subscription read on its own task, its quotes collected
	private sealed class Reader
	{
		public Task Running { get; set; } = Task.CompletedTask;
		public CancellationTokenSource Stop { get; } =
			CancellationTokenSource.CreateLinkedTokenSource(Ct);
		private readonly List<QuoteDto> _quotes = [];

		public void Add(QuoteDto quote)
		{
			lock (_quotes)
				_quotes.Add(quote);
		}

		public int Count
		{
			get
			{
				lock (_quotes)
					return _quotes.Count;
			}
		}

		public double? LastPrice
		{
			get
			{
				lock (_quotes)
					return _quotes.Count > 0 ? _quotes[^1].Last : null;
			}
		}
	}

	private static Reader Read(
		MarketDataManager manager,
		ContractDto contract,
		string owner,
		string genericTicks = ""
	)
	{
		var reader = new Reader();
		reader.Running = Task.Run(
			async () =>
			{
				await foreach (
					var quote in manager.SubscribeQuotesAsync(
						contract,
						reader.Stop.Token,
						genericTicks,
						owner
					)
				)
					reader.Add(quote);
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

	[Fact]
	public async Task A_second_subscriber_joins_the_open_line_with_the_latest_quote()
	{
		var manager = new MarketDataManager(_ibkr, TimeProvider.System);
		var watchlist = Read(manager, Aapl, MarketDataStreamOwners.Watchlist);
		await WaitUntil(() => watchlist.Count == 1);
		await _ibkr.MarketDataStreamGate!.Writer.WriteAsync(Last(101), Ct);
		await WaitUntil(() => watchlist.LastPrice == 101);

		var chart = Read(manager, Aapl, MarketDataStreamOwners.Chart);
		await WaitUntil(() => chart.Count == 1);

		Assert.Equal(101, chart.LastPrice);
		Assert.Single(_ibkr.MarketDataRequested);
		Assert.Equal(1, manager.ActiveLines);
		var stream = Assert.Single(manager.GetActiveStreams());
		Assert.Equal(
			[MarketDataStreamOwners.Watchlist, MarketDataStreamOwners.Chart],
			stream.Owners
		);
		Assert.Equal(
			$"{MarketDataStreamOwners.Watchlist}, {MarketDataStreamOwners.Chart}",
			stream.Owner
		);
		Assert.Equal(2, stream.Subscribers);

		// Every later tick reaches both
		await _ibkr.MarketDataStreamGate.Writer.WriteAsync(Last(102), Ct);
		await WaitUntil(() => watchlist.LastPrice == 102 && chart.LastPrice == 102);

		await watchlist.Stop.CancelAsync();
		await chart.Stop.CancelAsync();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => watchlist.Running);
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => chart.Running);
	}

	[Fact]
	public async Task Cancelling_one_subscriber_leaves_the_others_streaming()
	{
		var manager = new MarketDataManager(_ibkr, TimeProvider.System);
		var watchlist = Read(manager, Aapl, MarketDataStreamOwners.Watchlist);
		var chart = Read(manager, Aapl, MarketDataStreamOwners.Chart);
		await WaitUntil(() => watchlist.Count >= 1 && chart.Count >= 1);
		var changes = 0;
		manager.StreamsChanged += () => Interlocked.Increment(ref changes);

		await watchlist.Stop.CancelAsync();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => watchlist.Running);
		await _ibkr.MarketDataStreamGate!.Writer.WriteAsync(Last(105), Ct);
		await WaitUntil(() => chart.LastPrice == 105);

		Assert.Equal(1, manager.ActiveLines);
		Assert.Equal(1, _ibkr.OpenMarketDataStreams);
		var stream = Assert.Single(manager.GetActiveStreams());
		Assert.Equal([MarketDataStreamOwners.Chart], stream.Owners);
		Assert.Equal(1, stream.Subscribers);
		Assert.Equal(1, changes);

		// The last one leaving closes the line and IB's stream
		await chart.Stop.CancelAsync();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => chart.Running);
		Assert.Equal(0, manager.ActiveLines);
		Assert.Empty(manager.GetActiveStreams());
		await WaitUntil(() => _ibkr.OpenMarketDataStreams == 0);
		Assert.Equal(2, changes);
	}

	[Fact]
	public async Task An_ib_error_ends_every_subscriber_of_the_line_and_frees_it()
	{
		var manager = new MarketDataManager(_ibkr, TimeProvider.System);
		var watchlist = Read(manager, Aapl, MarketDataStreamOwners.Watchlist);
		var chart = Read(manager, Aapl, MarketDataStreamOwners.Chart);
		await WaitUntil(() => watchlist.Count >= 1 && chart.Count >= 1);

		_ibkr.MarketDataStreamGate!.Writer.Complete(
			new IbkrRequestException(354, "Requested market data is not subscribed.")
		);

		var first = await Assert.ThrowsAsync<IbkrRequestException>(() => watchlist.Running);
		var second = await Assert.ThrowsAsync<IbkrRequestException>(() => chart.Running);
		Assert.Equal((354, 354), (first.Code, second.Code));
		Assert.Equal(0, manager.ActiveLines);
		Assert.Empty(manager.GetActiveStreams());
	}

	[Fact]
	public async Task A_refused_line_fails_its_subscriber_and_the_next_one_asks_again()
	{
		var manager = new MarketDataManager(_ibkr, TimeProvider.System);
		_ibkr.MarketDataExceptionFor = _ => new IbkrRequestException(
			101,
			"Max number of tickers has been reached"
		);

		var refused = await Assert.ThrowsAsync<IbkrRequestException>(async () =>
			await manager.SubscribeQuotesAsync(Aapl, Ct).ToListAsync(Ct)
		);
		Assert.Equal(101, refused.Code);
		Assert.Equal(0, manager.ActiveLines);

		_ibkr.MarketDataExceptionFor = null;
		var reader = Read(manager, Aapl, MarketDataStreamOwners.Watchlist);
		await WaitUntil(() => reader.Count == 1);
		Assert.Equal(2, _ibkr.MarketDataRequested.Count);
		await reader.Stop.CancelAsync();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.Running);
	}

	[Fact]
	public async Task A_line_closed_by_its_last_subscriber_is_opened_again_for_the_next()
	{
		var manager = new MarketDataManager(_ibkr, TimeProvider.System);
		var first = Read(manager, Aapl, MarketDataStreamOwners.Watchlist);
		await WaitUntil(() => first.Count == 1);
		await first.Stop.CancelAsync();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.Running);

		var second = Read(manager, Aapl, MarketDataStreamOwners.Watchlist);
		await WaitUntil(() => second.Count == 1);

		Assert.Equal(2, _ibkr.MarketDataRequested.Count);
		Assert.Equal(1, manager.ActiveLines);
		await second.Stop.CancelAsync();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second.Running);
	}

	[Fact]
	public async Task Other_generic_ticks_another_listing_or_market_data_type_take_their_own_line()
	{
		var manager = new MarketDataManager(_ibkr, TimeProvider.System);
		_ibkr.MarketDataType = MarketDataType.Live;
		var readers = new List<Reader>
		{
			Read(manager, Aapl, MarketDataStreamOwners.Watchlist),
			Read(
				manager,
				Aapl,
				MarketDataStreamOwners.OptionChainUnderlying,
				MarketDataConstants.OptionImpliedVolatilityGenericTick
			),
			Read(
				manager,
				new ContractDto
				{
					ConId = Aapl.ConId,
					Symbol = "AAPL",
					Exchange = "NASDAQ",
				},
				MarketDataStreamOwners.Chart
			),
		};
		await WaitUntil(() => readers.All(x => x.Count == 1));
		_ibkr.MarketDataType = MarketDataType.Delayed;
		readers.Add(Read(manager, Aapl, MarketDataStreamOwners.Chart));
		await WaitUntil(() => readers.All(x => x.Count == 1));

		Assert.Equal(4, manager.ActiveLines);
		Assert.Equal(4, _ibkr.MarketDataRequested.Count);
		foreach (var reader in readers)
			await reader.Stop.CancelAsync();
		foreach (var reader in readers)
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.Running);
		Assert.Equal(0, manager.ActiveLines);
	}

	[Fact]
	public async Task A_snapshot_folds_its_ticks_into_a_quote_without_taking_a_line()
	{
		var manager = new MarketDataManager(_ibkr, TimeProvider.System);
		_ibkr.SnapshotGate = new TaskCompletionSource(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		_ibkr.SnapshotTicks = _ =>
			[
				new MarketDataTickDto { Field = TickTypeHelpers.Bid, Price = 99 },
				new MarketDataTickDto { Field = TickTypeHelpers.Last, Price = 100 },
				new MarketDataTickDto { Field = TickTypeHelpers.Close, Price = 98 },
			];

		var snapshot = manager.GetQuoteSnapshotAsync(Aapl, Ct);
		await WaitUntil(() => manager.ActiveSnapshots == 1);
		Assert.Equal(0, manager.ActiveLines);
		Assert.Empty(manager.GetActiveStreams());
		_ibkr.SnapshotGate.SetResult();
		var quote = await snapshot;

		Assert.Equal((99, 100, 98), (quote.Bid, quote.Last, quote.Close));
		Assert.Equal(0, manager.ActiveSnapshots);
		Assert.Same(Aapl, Assert.Single(_ibkr.SnapshotsRequested));
	}

	[Fact]
	public async Task A_snapshot_of_a_streamed_contract_is_the_lines_latest_quote()
	{
		var manager = new MarketDataManager(_ibkr, TimeProvider.System);
		var reader = Read(manager, Aapl, MarketDataStreamOwners.Chart);
		await WaitUntil(() => reader.Count == 1);

		var quote = await manager.GetQuoteSnapshotAsync(Aapl, Ct);

		Assert.Equal(100, quote.Last);
		Assert.Empty(_ibkr.SnapshotsRequested);
		await reader.Stop.CancelAsync();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.Running);
	}

	[Fact]
	public async Task A_snapshot_ib_does_not_end_times_out()
	{
		var time = new FakeTimeProvider();
		var manager = new MarketDataManager(_ibkr, time);
		_ibkr.SnapshotGate = new TaskCompletionSource();

		var snapshot = manager.GetQuoteSnapshotAsync(Aapl, Ct);
		await WaitUntil(() => _ibkr.SnapshotsRequested.Count == 1);
		time.Advance(MarketDataConstants.SnapshotTimeout);

		var error = await Assert.ThrowsAsync<LSCoreBadRequestException>(() => snapshot);
		Assert.Contains("timed out", error.Message);
		Assert.Equal(0, manager.ActiveSnapshots);
	}

	[Fact]
	public async Task Snapshots_above_the_limit_are_refused_and_ib_errors_pass_through()
	{
		var manager = new MarketDataManager(_ibkr, TimeProvider.System);
		_ibkr.SnapshotGate = new TaskCompletionSource(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		var running = Enumerable
			.Range(0, MarketDataConstants.MaxConcurrentSnapshots)
			.Select(i => manager.GetQuoteSnapshotAsync(new ContractDto { ConId = i + 1 }, Ct))
			.ToList();
		await WaitUntil(() =>
			manager.ActiveSnapshots == MarketDataConstants.MaxConcurrentSnapshots
		);

		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			manager.GetQuoteSnapshotAsync(Aapl, Ct)
		);

		_ibkr.SnapshotExceptionFor = _ => new IbkrRequestException(354, "Not subscribed.");
		_ibkr.SnapshotGate.SetResult();
		foreach (var task in running)
			Assert.Equal(354, (await Assert.ThrowsAsync<IbkrRequestException>(() => task)).Code);
		Assert.Equal(0, manager.ActiveSnapshots);
	}
}
