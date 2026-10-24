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
		Assert.Equal(("AAPL", "STK"), (_ibkr.LastSearch!.Symbol, _ibkr.LastSearch.SecurityType));
		Assert.Equal(["AAPL"], manager.GetRecentContracts().Select(x => x.Symbol));
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
