using LSCore.Exceptions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.Managers;

public class MarketDataManagerTests
{
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private static readonly ContractDto Aapl = new() { ConId = 265598, Symbol = "AAPL" };

	[Fact]
	public async Task Folds_ticks_into_cumulative_quotes_and_skips_unknown_fields()
	{
		_ibkr.Ticks =
		[
			new MarketDataTickDto { Field = TickTypeHelpers.Bid, Price = 99.5 },
			new MarketDataTickDto { Field = 999, Value = 1 },
			new MarketDataTickDto { Field = TickTypeHelpers.Ask, Price = 100.5 },
		];

		var quotes = await new MarketDataManager(_ibkr, TimeProvider.System)
			.SubscribeQuotesAsync(Aapl, TestContext.Current.CancellationToken)
			.ToListAsync(TestContext.Current.CancellationToken);

		Assert.Equal(2, quotes.Count);
		Assert.Equal((99.5, 100.5), (quotes[^1].Bid, quotes[^1].Ask));
		Assert.Null(quotes[0].Ask);
	}

	[Fact]
	public async Task Generic_ticks_ride_on_the_same_line()
	{
		_ibkr.Ticks =
		[
			new MarketDataTickDto { Field = TickTypeHelpers.Last, Price = 200 },
			new MarketDataTickDto { Field = TickTypeHelpers.OptionImpliedVolatility, Value = 0.25 },
		];
		_ibkr.KeepMarketDataOpen = true;
		var manager = new MarketDataManager(_ibkr, TimeProvider.System);
		var maxLines = 0;
		var quotes = 0;

		await foreach (
			var _ in manager.SubscribeQuotesAsync(
				Aapl,
				TestContext.Current.CancellationToken,
				MarketDataConstants.OptionImpliedVolatilityGenericTick
			)
		)
		{
			maxLines = Math.Max(maxLines, manager.ActiveLines);
			if (++quotes == 2)
				break;
		}

		Assert.Equal(["106"], _ibkr.GenericTicksRequested);
		Assert.Equal(1, maxLines);
	}

	[Fact]
	public async Task Releases_the_line_when_the_stream_ends()
	{
		var manager = new MarketDataManager(_ibkr, TimeProvider.System);

		await manager
			.SubscribeQuotesAsync(Aapl, TestContext.Current.CancellationToken)
			.ToListAsync(TestContext.Current.CancellationToken);

		Assert.Equal(0, manager.ActiveLines);
	}

	[Fact]
	public async Task Refuses_more_than_the_line_limit()
	{
		var manager = new MarketDataManager(_ibkr, TimeProvider.System);
		_ibkr.Ticks = [new MarketDataTickDto { Field = TickTypeHelpers.Bid, Price = 1 }];
		_ibkr.KeepMarketDataOpen = true;
		var open = new List<IAsyncEnumerator<QuoteDto>>();
		// Different contracts: the same one would share a line
		for (var i = 0; i < MarketDataConstants.MaxLines; i++)
		{
			var enumerator = manager
				.SubscribeQuotesAsync(
					new ContractDto { ConId = i + 1, Symbol = $"S{i}" },
					TestContext.Current.CancellationToken
				)
				.GetAsyncEnumerator(TestContext.Current.CancellationToken);
			await enumerator.MoveNextAsync();
			open.Add(enumerator);
		}

		await Assert.ThrowsAsync<LSCoreBadRequestException>(async () =>
			await manager
				.SubscribeQuotesAsync(Aapl, TestContext.Current.CancellationToken)
				.GetAsyncEnumerator(TestContext.Current.CancellationToken)
				.MoveNextAsync()
		);
		Assert.Equal(MarketDataConstants.MaxLines, manager.ActiveLines);
		// An open contract still joins its line at the limit
		await using (
			var shared = manager
				.SubscribeQuotesAsync(
					new ContractDto { ConId = 1, Symbol = "S0" },
					TestContext.Current.CancellationToken
				)
				.GetAsyncEnumerator(TestContext.Current.CancellationToken)
		)
			Assert.True(await shared.MoveNextAsync());
		Assert.Equal(MarketDataConstants.MaxLines, manager.ActiveLines);
		Assert.Equal(MarketDataConstants.MaxLines, manager.GetActiveStreams().Count);
		Assert.Throws<LSCoreBadRequestException>(() =>
			manager.AcquireLine(MarketDataStreamKind.News, Aapl, MarketDataStreamOwners.News)
		);
		Assert.Equal(MarketDataConstants.MaxLines, manager.GetActiveStreams().Count);

		foreach (var enumerator in open)
			await enumerator.DisposeAsync();
		Assert.Equal(0, manager.ActiveLines);
		Assert.Empty(manager.GetActiveStreams());
	}

	[Fact]
	public async Task An_open_quote_stream_is_listed_with_its_owner_and_open_time()
	{
		var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 27, 14, 30, 0, TimeSpan.Zero));
		var manager = new MarketDataManager(_ibkr, time);
		_ibkr.Ticks = [new MarketDataTickDto { Field = TickTypeHelpers.Bid, Price = 1 }];
		_ibkr.KeepMarketDataOpen = true;
		var changes = 0;
		manager.StreamsChanged += () => changes++;

		await using var quotes = manager
			.SubscribeQuotesAsync(
				Aapl,
				TestContext.Current.CancellationToken,
				MarketDataConstants.OptionImpliedVolatilityGenericTick,
				MarketDataStreamOwners.OptionChain
			)
			.GetAsyncEnumerator(TestContext.Current.CancellationToken);
		Assert.Empty(manager.GetActiveStreams());
		Assert.True(await quotes.MoveNextAsync());

		var stream = Assert.Single(manager.GetActiveStreams());
		Assert.Equal(MarketDataStreamKind.Quote, stream.Kind);
		Assert.Same(Aapl, stream.Contract);
		Assert.Equal("106", stream.GenericTicks);
		Assert.Equal(MarketDataStreamOwners.OptionChain, stream.Owner);
		Assert.Equal(time.GetUtcNow(), stream.OpenedAt);
		Assert.Equal(1, changes);

		await quotes.DisposeAsync();
		Assert.Empty(manager.GetActiveStreams());
		Assert.Equal(2, changes);
	}

	[Fact]
	public async Task A_cancelled_quote_stream_leaves_the_list()
	{
		var manager = new MarketDataManager(_ibkr, TimeProvider.System);
		_ibkr.Ticks = [new MarketDataTickDto { Field = TickTypeHelpers.Bid, Price = 1 }];
		_ibkr.KeepMarketDataOpen = true;
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(
			TestContext.Current.CancellationToken
		);
		var seen = new TaskCompletionSource();
		var running = Task.Run(
			async () =>
			{
				await foreach (
					var _ in manager.SubscribeQuotesAsync(
						Aapl,
						cts.Token,
						owner: MarketDataStreamOwners.Watchlist
					)
				)
					seen.TrySetResult();
			},
			TestContext.Current.CancellationToken
		);
		await seen.Task.WaitAsync(TestContext.Current.CancellationToken);
		Assert.Equal(
			MarketDataStreamOwners.Watchlist,
			Assert.Single(manager.GetActiveStreams()).Owner
		);

		await cts.CancelAsync();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
		Assert.Empty(manager.GetActiveStreams());
		Assert.Equal(0, manager.ActiveLines);
	}

	[Fact]
	public void A_line_taken_by_hand_is_listed_until_released_once()
	{
		var manager = new MarketDataManager(_ibkr, TimeProvider.System);
		var tape = new ContractDto { Symbol = "BRFG:BRFG_ALL", SecurityType = "NEWS" };
		var changes = 0;
		manager.StreamsChanged += () => changes++;

		var id = manager.AcquireLine(MarketDataStreamKind.News, tape, MarketDataStreamOwners.News);
		var other = manager.AcquireLine(MarketDataStreamKind.News, Aapl);

		Assert.Equal([id, other], manager.GetActiveStreams().Select(x => x.Id));
		var stream = manager.GetActiveStreams()[0];
		Assert.Equal(
			(MarketDataStreamKind.News, MarketDataStreamOwners.News),
			(stream.Kind, stream.Owner)
		);
		Assert.Same(tape, stream.Contract);

		manager.ReleaseLine(id);
		manager.ReleaseLine(id);
		manager.ReleaseLine(12345);

		Assert.Equal(other, Assert.Single(manager.GetActiveStreams()).Id);
		Assert.Equal(1, manager.ActiveLines);
		Assert.Equal(3, changes);
	}

	[Fact]
	public async Task Throws_when_not_connected()
	{
		_ibkr.IsConnected = false;

		await Assert.ThrowsAsync<LSCoreBadRequestException>(async () =>
			await new MarketDataManager(_ibkr, TimeProvider.System)
				.SubscribeQuotesAsync(Aapl, TestContext.Current.CancellationToken)
				.ToListAsync(TestContext.Current.CancellationToken)
		);
	}

	[Fact]
	public async Task Tick_by_tick_subscriptions_are_counted_against_ibs_limit_of_5()
	{
		var manager = new MarketDataManager(_ibkr, TimeProvider.System);
		_ibkr.KeepTickByTickOpen = true;
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(
			TestContext.Current.CancellationToken
		);
		var open = Enumerable
			.Range(0, MarketDataConstants.MaxTickByTick)
			.Select(_ =>
				manager
					.SubscribeTickByTickAsync(Aapl, TickByTickType.Last, cts.Token)
					.ToListAsync(cts.Token)
					.AsTask()
			)
			.ToList();
		while (manager.ActiveTickByTick < MarketDataConstants.MaxTickByTick)
			await Task.Delay(5, TestContext.Current.CancellationToken);

		await Assert.ThrowsAsync<LSCoreBadRequestException>(async () =>
			await manager
				.SubscribeTickByTickAsync(
					Aapl,
					TickByTickType.Last,
					TestContext.Current.CancellationToken
				)
				.ToListAsync(TestContext.Current.CancellationToken)
		);

		await cts.CancelAsync();
		foreach (var task in open)
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
		Assert.Equal(0, manager.ActiveTickByTick);
	}
}
