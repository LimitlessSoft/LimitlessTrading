using LSCore.Exceptions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Requests.Contracts;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.Managers;

public class ContractManagerTests
{
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private readonly InMemorySettingsRepository _settings = new();
	private readonly InMemoryCacheRepository _cache = new();
	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 23, 15, 0, 0, TimeSpan.Zero)
	);
	private readonly CacheManager _cacheManager;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private static ContractDto Stock(int conId, string symbol) =>
		new()
		{
			ConId = conId,
			Symbol = symbol,
			SecurityType = "STK",
			Currency = "USD",
		};

	public ContractManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_cacheManager = new CacheManager(_cache, _settings, _time);
	}

	[Fact]
	public async Task Stocks_are_searched_via_smart_and_upper_cased()
	{
		await new ContractManager(_ibkr, _settings, _cacheManager).SearchAsync(
			new ContractSearchRequest { Symbol = " aapl ", SecurityType = "STK" },
			TestContext.Current.CancellationToken
		);

		Assert.Equal(("AAPL", "SMART"), (_ibkr.LastSearch!.Symbol, _ibkr.LastSearch.Exchange));
	}

	[Fact]
	public async Task Futures_search_every_exchange()
	{
		await new ContractManager(_ibkr, _settings, _cacheManager).SearchAsync(
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
			new ContractManager(_ibkr, _settings, _cacheManager).SearchAsync(
				new ContractSearchRequest { Symbol = symbol, SecurityType = type },
				TestContext.Current.CancellationToken
			)
		);

	[Fact]
	public async Task ResolveTicker_returns_the_stock_and_remembers_it()
	{
		_ibkr.SearchResults = [new ContractDetailsDto { Contract = Stock(265598, "AAPL") }];
		var manager = new ContractManager(_ibkr, _settings, _cacheManager);

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

		var contract = await new ContractManager(
			_ibkr,
			_settings,
			_cacheManager
		).ResolveTickerAsync("SPY", Ct);

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

		var results = await new ContractManager(_ibkr, _settings, _cacheManager).SearchSymbolAsync(
			"BTC",
			Ct
		);

		Assert.Equal(["STK", "CRYPTO"], results.Select(x => x.Contract.SecurityType));
	}

	[Fact]
	public async Task Symbol_search_fails_only_when_every_type_fails()
	{
		_ibkr.SearchBy = _ => throw new InvalidOperationException("Not connected");

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			new ContractManager(_ibkr, _settings, _cacheManager).SearchSymbolAsync("SPY", Ct)
		);
	}

	[Fact]
	public async Task ResolveTicker_with_no_match_is_rejected() =>
		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			new ContractManager(_ibkr, _settings, _cacheManager).ResolveTickerAsync("NOPE", Ct)
		);

	[Fact]
	public void Recent_contracts_are_newest_first_without_duplicates_and_capped()
	{
		var manager = new ContractManager(_ibkr, _settings, _cacheManager);
		for (var i = 1; i <= 12; i++)
			manager.AddRecentContract(Stock(i, $"S{i}"));
		manager.AddRecentContract(Stock(5, "S5"));

		var recent = manager.GetRecentContracts().Select(x => x.Symbol).ToList();

		Assert.Equal(10, recent.Count);
		Assert.Equal(["S5", "S12", "S11"], recent.Take(3));
		Assert.Single(recent, x => x == "S5");
	}

	private ContractManager Manager() => new(_ibkr, _settings, _cacheManager);

	private static readonly ContractSearchRequest AaplSearch = new()
	{
		Symbol = "AAPL",
		SecurityType = "STK",
	};

	[Fact]
	public async Task Search_is_served_from_the_cache_for_days()
	{
		_ibkr.SearchResults = [new ContractDetailsDto { Contract = Stock(265598, "AAPL") }];
		await Manager().SearchAsync(AaplSearch, Ct);
		_time.Advance(TimeSpan.FromDays(2));

		var results = await Manager()
			.SearchAsync(new ContractSearchRequest { Symbol = " aapl ", SecurityType = "STK" }, Ct);

		Assert.Equal(1, _ibkr.SearchCalls);
		Assert.Equal(265598, Assert.Single(results).Contract.ConId);
	}

	[Fact]
	public async Task Search_is_asked_again_once_expired()
	{
		_ibkr.SearchResults = [new ContractDetailsDto { Contract = Stock(265598, "AAPL") }];
		await Manager().SearchAsync(AaplSearch, Ct);
		_time.Advance(CacheConstants.ContractTtl);

		await Manager().SearchAsync(AaplSearch, Ct);

		Assert.Equal(2, _ibkr.SearchCalls);
	}

	[Fact]
	public async Task Expired_search_is_served_when_ib_fails()
	{
		_ibkr.SearchResults = [new ContractDetailsDto { Contract = Stock(265598, "AAPL") }];
		await Manager().SearchAsync(AaplSearch, Ct);
		_time.Advance(CacheConstants.ContractTtl * 2);
		_ibkr.SearchException = new InvalidOperationException("Not connected");

		var results = await Manager().SearchAsync(AaplSearch, Ct);

		Assert.Single(results);
	}

	[Fact]
	public async Task Empty_answers_are_not_cached()
	{
		await Manager().SearchAsync(AaplSearch, Ct);
		await Manager().SearchAsync(AaplSearch, Ct);

		Assert.Equal(2, _ibkr.SearchCalls);
		Assert.Empty(_cache.Entries);
	}

	[Fact]
	public async Task Disabled_contract_cache_always_asks_ib()
	{
		_settings.Settings.General.CacheContracts = false;
		_ibkr.SearchResults = [new ContractDetailsDto { Contract = Stock(265598, "AAPL") }];

		await Manager().SearchAsync(AaplSearch, Ct);
		await Manager().SearchAsync(AaplSearch, Ct);

		Assert.Equal(2, _ibkr.SearchCalls);
		Assert.Empty(_cache.Entries);
	}

	[Fact]
	public async Task Resolving_a_cached_ticker_asks_ib_nothing()
	{
		_ibkr.SearchBy = q =>
			q.SecurityType == "STK"
				? [new ContractDetailsDto { Contract = Stock(756733, "SPY") }]
				: throw new InvalidOperationException("No security definition");
		await Manager().ResolveTickerAsync("SPY", Ct);
		var calls = _ibkr.SearchCalls;

		var contract = await Manager().ResolveTickerAsync("spy", Ct);

		Assert.Equal(756733, contract.ConId);
		Assert.Equal(calls, _ibkr.SearchCalls);
	}
}
