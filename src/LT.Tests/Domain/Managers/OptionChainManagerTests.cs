using LSCore.Exceptions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.Options;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.Options;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

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

	private readonly InMemoryCacheRepository _cache = new();
	private readonly InMemorySettingsRepository _settings = new();
	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 23, 15, 0, 0, TimeSpan.Zero)
	);

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public OptionChainManagerTests() => LSCoreContainer.EnsureInitialized();

	private OptionChainManager Manager() =>
		new(
			_ibkr,
			new MarketDataManager(_ibkr, TimeProvider.System),
			new CacheManager(_cache, _settings, _time)
		);

	private static readonly GetOptionChainRequest NovemberChain = new()
	{
		Underlying = Aapl,
		Expiry = "202611",
	};

	private void ListAaplOptions()
	{
		_ibkr.OptionChainParameters =
		[
			new OptionChainParametersDto
			{
				Exchange = "SMART",
				TradingClass = "AAPL",
				Expirations = ["20261120"],
			},
		];
		_ibkr.SearchResults = [Option("20261120", 200, "C"), Option("20261120", 200, "P")];
	}

	[Fact]
	public async Task Expirations_are_served_from_the_cache_for_a_day()
	{
		ListAaplOptions();
		await Manager().GetExpirationsAsync(Aapl, Ct);
		_time.Advance(TimeSpan.FromHours(23));

		var expirations = await Manager().GetExpirationsAsync(Aapl, Ct);

		Assert.Equal(1, _ibkr.OptionParameterCalls);
		Assert.Equal("20261120", Assert.Single(expirations).Expiration);
	}

	[Fact]
	public async Task Expirations_are_asked_again_after_a_day()
	{
		ListAaplOptions();
		await Manager().GetExpirationsAsync(Aapl, Ct);
		_time.Advance(CacheConstants.OptionChainTtl);

		await Manager().GetExpirationsAsync(Aapl, Ct);

		Assert.Equal(2, _ibkr.OptionParameterCalls);
	}

	[Fact]
	public async Task Expired_expirations_are_served_when_ib_fails()
	{
		ListAaplOptions();
		await Manager().GetExpirationsAsync(Aapl, Ct);
		_time.Advance(TimeSpan.FromDays(3));
		_ibkr.OptionParametersException = new InvalidOperationException("Not connected");

		var expirations = await Manager().GetExpirationsAsync(Aapl, Ct);

		Assert.Single(expirations);
	}

	[Fact]
	public async Task Strikes_are_cached_but_the_underlying_price_is_always_live()
	{
		ListAaplOptions();
		_ibkr.Ticks = [new MarketDataTickDto { Field = TickTypeHelpers.Last, Price = 201.25 }];
		await Manager().GetChainAsync(NovemberChain, Ct);
		_ibkr.Ticks = [new MarketDataTickDto { Field = TickTypeHelpers.Last, Price = 205.5 }];

		var chain = await Manager().GetChainAsync(NovemberChain, Ct);

		Assert.Equal(1, _ibkr.SearchCalls);
		Assert.Equal(205.5, chain.UnderlyingPrice);
		Assert.Equal(200, Assert.Single(chain.Strikes).Strike);
		Assert.DoesNotContain(_cache.Entries.Values, x => x.Json.Contains("205.5"));
		Assert.DoesNotContain(_cache.Entries.Values, x => x.Json.Contains("201.25"));
	}

	[Fact]
	public async Task Disabled_option_chain_cache_always_asks_ib()
	{
		ListAaplOptions();
		_settings.Settings.General.CacheOptionChains = false;

		await Manager().GetExpirationsAsync(Aapl, Ct);
		await Manager().GetChainAsync(NovemberChain, Ct);
		await Manager().GetExpirationsAsync(Aapl, Ct);
		await Manager().GetChainAsync(NovemberChain, Ct);

		Assert.Equal(2, _ibkr.OptionParameterCalls);
		Assert.Equal(2, _ibkr.SearchCalls);
		Assert.Empty(_cache.Entries);
	}

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

		var chain = await Manager()
			.GetChainAsync(
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
	public async Task Expirations_come_from_the_option_parameters()
	{
		_ibkr.OptionChainParameters =
		[
			new OptionChainParametersDto
			{
				Exchange = "SMART",
				TradingClass = "AAPL",
				Expirations = ["20261127", "20261120"],
			},
		];

		var expirations = await Manager()
			.GetExpirationsAsync(Aapl, TestContext.Current.CancellationToken);

		Assert.Equal(["20261120", "20261127"], expirations.Select(x => x.Expiration));
	}

	[Fact]
	public async Task No_listed_options_is_reported() =>
		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			Manager().GetExpirationsAsync(Aapl, TestContext.Current.CancellationToken)
		);

	[Fact]
	public async Task Empty_chain_is_reported() =>
		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			Manager()
				.GetChainAsync(
					new GetOptionChainRequest { Underlying = Aapl, Expiry = "202611" },
					TestContext.Current.CancellationToken
				)
		);

	[Theory]
	[InlineData("2026-11")]
	[InlineData("2611")]
	public async Task Bad_expiry_is_rejected(string expiry) =>
		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			Manager()
				.GetChainAsync(
					new GetOptionChainRequest { Underlying = Aapl, Expiry = expiry },
					TestContext.Current.CancellationToken
				)
		);

	[Fact]
	public async Task Futures_have_no_chain() =>
		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			Manager()
				.GetChainAsync(
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
