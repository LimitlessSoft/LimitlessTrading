using LSCore.Exceptions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Helpers;
using LT.Domain.Managers;
using LT.Tests.Fakes;

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

		var quotes = await new MarketDataManager(_ibkr)
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
		var manager = new MarketDataManager(_ibkr);
		var maxLines = 0;

		await foreach (
			var _ in manager.SubscribeQuotesAsync(
				Aapl,
				TestContext.Current.CancellationToken,
				MarketDataConstants.OptionImpliedVolatilityGenericTick
			)
		)
			maxLines = Math.Max(maxLines, manager.ActiveLines);

		Assert.Equal(["106"], _ibkr.GenericTicksRequested);
		Assert.Equal(1, maxLines);
	}

	[Fact]
	public async Task Releases_the_line_when_the_stream_ends()
	{
		var manager = new MarketDataManager(_ibkr);

		await manager
			.SubscribeQuotesAsync(Aapl, TestContext.Current.CancellationToken)
			.ToListAsync(TestContext.Current.CancellationToken);

		Assert.Equal(0, manager.ActiveLines);
	}

	[Fact]
	public async Task Refuses_more_than_the_line_limit()
	{
		var manager = new MarketDataManager(_ibkr);
		_ibkr.Ticks = [new MarketDataTickDto { Field = TickTypeHelpers.Bid, Price = 1 }];
		var open = new List<IAsyncEnumerator<QuoteDto>>();
		for (var i = 0; i < MarketDataConstants.MaxLines; i++)
		{
			var enumerator = manager
				.SubscribeQuotesAsync(Aapl, TestContext.Current.CancellationToken)
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

		foreach (var enumerator in open)
			await enumerator.DisposeAsync();
		Assert.Equal(0, manager.ActiveLines);
	}

	[Fact]
	public async Task Throws_when_not_connected()
	{
		_ibkr.IsConnected = false;

		await Assert.ThrowsAsync<LSCoreBadRequestException>(async () =>
			await new MarketDataManager(_ibkr)
				.SubscribeQuotesAsync(Aapl, TestContext.Current.CancellationToken)
				.ToListAsync(TestContext.Current.CancellationToken)
		);
	}
}
