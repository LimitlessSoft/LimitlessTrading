using LSCore.Exceptions;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Requests.Contracts;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

public class ContractManagerTests
{
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private readonly InMemorySettingsRepository _settings = new();

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private static ContractDto Stock(int conId, string symbol) =>
		new()
		{
			ConId = conId,
			Symbol = symbol,
			SecurityType = "STK",
			Currency = "USD",
		};

	public ContractManagerTests() => LSCoreContainer.EnsureInitialized();

	[Fact]
	public async Task Stocks_are_searched_via_smart_and_upper_cased()
	{
		await new ContractManager(_ibkr, _settings).SearchAsync(
			new ContractSearchRequest { Symbol = " aapl ", SecurityType = "STK" },
			TestContext.Current.CancellationToken
		);

		Assert.Equal(("AAPL", "SMART"), (_ibkr.LastSearch!.Symbol, _ibkr.LastSearch.Exchange));
	}

	[Fact]
	public async Task Futures_search_every_exchange()
	{
		await new ContractManager(_ibkr, _settings).SearchAsync(
			new ContractSearchRequest { Symbol = "ES", SecurityType = "FUT" },
			TestContext.Current.CancellationToken
		);

		Assert.Equal(string.Empty, _ibkr.LastSearch!.Exchange);
	}

	[Theory]
	[InlineData("", "STK")]
	[InlineData("AAPL", "OPT")]
	public async Task Invalid_search_is_rejected(string symbol, string type) =>
		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			new ContractManager(_ibkr, _settings).SearchAsync(
				new ContractSearchRequest { Symbol = symbol, SecurityType = type },
				TestContext.Current.CancellationToken
			)
		);

	[Fact]
	public async Task ResolveTicker_returns_the_stock_and_remembers_it()
	{
		_ibkr.SearchResults = [new ContractDetailsDto { Contract = Stock(265598, "AAPL") }];
		var manager = new ContractManager(_ibkr, _settings);

		var contract = await manager.ResolveTickerAsync(" aapl ", Ct);

		Assert.Equal(265598, contract.ConId);
		Assert.Equal("AAPL", _ibkr.LastSearch!.Symbol);
		Assert.Equal(["AAPL"], manager.GetRecentContracts().Select(x => x.Symbol));
	}

	[Fact]
	public async Task ResolveTicker_prefers_the_US_dollar_listing()
	{
		var asx = Stock(237937002, "SPY");
		asx.Currency = "AUD";
		_ibkr.SearchResults =
		[
			new ContractDetailsDto { Contract = asx },
			new ContractDetailsDto { Contract = Stock(756733, "SPY") },
		];

		var contract = await new ContractManager(_ibkr, _settings).ResolveTickerAsync("SPY", Ct);

		Assert.Equal(756733, contract.ConId);
	}

	[Fact]
	public async Task Symbol_search_merges_stocks_indices_and_crypto_in_that_order()
	{
		_ibkr.SearchBy = query =>
			query.SecurityType switch
			{
				"STK" => [new ContractDetailsDto { Contract = Stock(1, "BTC") }],
				"CRYPTO" =>
				[
					new ContractDetailsDto
					{
						Contract = new ContractDto
						{
							ConId = 2,
							Symbol = "BTC",
							SecurityType = "CRYPTO",
						},
					},
				],
				_ => throw new InvalidOperationException("No security definition"),
			};

		var results = await new ContractManager(_ibkr, _settings).SearchSymbolAsync("BTC", Ct);

		Assert.Equal(["STK", "CRYPTO"], results.Select(x => x.Contract.SecurityType));
	}

	[Fact]
	public async Task Symbol_search_fails_only_when_every_type_fails()
	{
		_ibkr.SearchBy = _ => throw new InvalidOperationException("Not connected");

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			new ContractManager(_ibkr, _settings).SearchSymbolAsync("SPY", Ct)
		);
	}

	[Fact]
	public async Task ResolveTicker_with_no_match_is_rejected() =>
		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			new ContractManager(_ibkr, _settings).ResolveTickerAsync("NOPE", Ct)
		);

	[Fact]
	public void Recent_contracts_are_newest_first_without_duplicates_and_capped()
	{
		var manager = new ContractManager(_ibkr, _settings);
		for (var i = 1; i <= 12; i++)
			manager.AddRecentContract(Stock(i, $"S{i}"));
		manager.AddRecentContract(Stock(5, "S5"));

		var recent = manager.GetRecentContracts().Select(x => x.Symbol).ToList();

		Assert.Equal(10, recent.Count);
		Assert.Equal(["S5", "S12", "S11"], recent.Take(3));
		Assert.Single(recent, x => x == "S5");
	}
}
