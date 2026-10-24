using LSCore.Exceptions;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.Options;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

public class OptionChainManagerTests
{
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private static readonly ContractDto Aapl = new()
	{
		ConId = 265598,
		Symbol = "AAPL",
		SecurityType = "STK",
		Currency = "USD",
	};

	public OptionChainManagerTests() => LSCoreContainer.EnsureInitialized();

	private static ContractDetailsDto Option(string expiry, double strike, string right) =>
		new()
		{
			Contract = new ContractDto
			{
				ConId = (int)strike * 10 + (right == "C" ? 1 : 2),
				Symbol = "AAPL",
				SecurityType = "OPT",
				LastTradeDateOrContractMonth = expiry,
				Strike = strike,
				Right = right,
			},
		};

	[Fact]
	public async Task Builds_the_chain_and_reads_the_underlying_price()
	{
		_ibkr.SearchResults =
		[
			Option("20261120", 200, "C"),
			Option("20261120", 200, "P"),
			Option("20261127", 210, "C"),
		];
		_ibkr.Ticks = [new MarketDataTickDto { Field = TickTypeHelpers.Last, Price = 201.25 }];

		var chain = await new OptionChainManager(_ibkr, new MarketDataManager(_ibkr)).GetChainAsync(
			new GetOptionChainRequest { Underlying = Aapl, Expiry = "202611" },
			TestContext.Current.CancellationToken
		);

		Assert.Equal(["20261120", "20261127"], chain.Expirations);
		Assert.Equal(201.25, chain.UnderlyingPrice);
		var first = chain.Strikes[0];
		Assert.NotNull(first.Call);
		Assert.NotNull(first.Put);
		Assert.Equal(
			("OPT", "SMART", "202611"),
			(
				_ibkr.LastSearch!.SecurityType,
				_ibkr.LastSearch.Exchange,
				_ibkr.LastSearch.LastTradeDateOrContractMonth
			)
		);
	}

	[Fact]
	public async Task Empty_chain_is_reported() =>
		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			new OptionChainManager(_ibkr, new MarketDataManager(_ibkr)).GetChainAsync(
				new GetOptionChainRequest { Underlying = Aapl, Expiry = "202611" },
				TestContext.Current.CancellationToken
			)
		);

	[Theory]
	[InlineData("2026-11")]
	[InlineData("2611")]
	public async Task Bad_expiry_is_rejected(string expiry) =>
		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			new OptionChainManager(_ibkr, new MarketDataManager(_ibkr)).GetChainAsync(
				new GetOptionChainRequest { Underlying = Aapl, Expiry = expiry },
				TestContext.Current.CancellationToken
			)
		);

	[Fact]
	public async Task Futures_have_no_chain() =>
		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			new OptionChainManager(_ibkr, new MarketDataManager(_ibkr)).GetChainAsync(
				new GetOptionChainRequest
				{
					Underlying = new ContractDto
					{
						ConId = 1,
						Symbol = "ES",
						SecurityType = "FUT",
					},
					Expiry = "202612",
				},
				TestContext.Current.CancellationToken
			)
		);
}
