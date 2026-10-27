using LSCore.Exceptions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.News;
using LT.Contracts.Requests.News;
using LT.Domain.Managers;
using LT.Domain.NewsSources;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.NewsSources;

public class IbkrNewsSourceTests
{
	private static readonly ContractDto Aapl = new() { ConId = 265598, Symbol = "AAPL" };
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private readonly MarketDataManager _marketData;
	private readonly IbkrNewsSource _source;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public IbkrNewsSourceTests()
	{
		_marketData = new MarketDataManager(_ibkr);
		_source = new IbkrNewsSource(_ibkr, _marketData);
	}

	private static NewsProviderDto Provider(string code) =>
		new() { SourceId = NewsConstants.IbkrSourceId, Code = code };

	private static NewsHeadlineDto Headline(string articleId, string providerCode = "DJ-N") =>
		new()
		{
			ProviderCode = providerCode,
			ArticleId = articleId,
			Time = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero),
		};

	[Fact]
	public async Task Providers_and_headlines_are_marked_with_the_source_and_contract()
	{
		_ibkr.NewsProviders = [new NewsProviderDto { Code = "DJ-N", Name = "Dow Jones" }];
		_ibkr.HistoricalNews = [Headline("1")];

		var providers = await _source.GetProvidersAsync(Ct);
		var page = await _source.GetHeadlinesAsync(
			new GetNewsHeadlinesRequest { Contract = Aapl, Providers = providers },
			Ct
		);

		Assert.Equal(NewsConstants.IbkrSourceId, providers[0].SourceId);
		var headline = Assert.Single(page.Headlines);
		Assert.Equal(
			(NewsConstants.IbkrSourceId, 265598, "AAPL"),
			(headline.SourceId, headline.ContractId, headline.Symbol)
		);
		var request = Assert.Single(_ibkr.NewsRequests);
		Assert.Equal((265598, NewsConstants.HeadlinesPageSize), (request.ContractId, request.Max));
		Assert.Equal(["DJ-N"], request.Providers);
	}

	[Fact]
	public async Task Other_sources_providers_are_not_sent_to_ib()
	{
		var page = await _source.GetHeadlinesAsync(
			new GetNewsHeadlinesRequest
			{
				Contract = Aapl,
				Providers = [new NewsProviderDto { SourceId = "rss", Code = "Reuters" }],
			},
			Ct
		);

		Assert.Empty(page.Headlines);
		Assert.Empty(_ibkr.NewsRequests);
	}

	[Fact]
	public async Task A_contract_is_one_subscription_with_its_providers_on_one_line()
	{
		_ibkr.LiveNews = [Headline("1")];
		await using var live = _source
			.SubscribeHeadlinesAsync(
				new SubscribeNewsRequest
				{
					Contract = Aapl,
					Providers = [Provider("DJ-N"), Provider("BRFG")],
				},
				Ct
			)
			.GetAsyncEnumerator(Ct);

		Assert.True(await live.MoveNextAsync());
		Assert.Equal("AAPL", live.Current.Symbol);
		Assert.Equal(1, _marketData.ActiveLines);
		var subscription = Assert.Single(_ibkr.NewsSubscriptions);
		Assert.Equal(["DJ-N", "BRFG"], subscription.Providers!);

		await live.DisposeAsync();
		Assert.Equal(0, _marketData.ActiveLines);
	}

	[Fact]
	public async Task Broad_tapes_subscribe_the_all_tape_of_each_provider_that_has_one()
	{
		_ibkr.BroadTapes = new()
		{
			["BRFG"] =
			[
				new ContractDto
				{
					ConId = 1,
					Symbol = "BRFG:BRFG_SMU",
					SecurityType = "NEWS",
				},
				new ContractDto
				{
					ConId = 2,
					Symbol = "BRFG:BRFG_ALL",
					SecurityType = "NEWS",
				},
			],
			["DJNL"] =
			[
				new ContractDto
				{
					ConId = 3,
					Symbol = "DJNL:DJNL_ALL",
					SecurityType = "NEWS",
				},
			],
		};
		_ibkr.LiveNews = [Headline("1")];
		await using var live = _source
			.SubscribeHeadlinesAsync(
				new SubscribeNewsRequest
				{
					Providers = [Provider("BRFG"), Provider("BZ"), Provider("DJNL")],
				},
				Ct
			)
			.GetAsyncEnumerator(Ct);

		Assert.True(await live.MoveNextAsync());
		// The two tapes are pumped in parallel: the first headline can beat the other subscription
		await _ibkr.WaitForNewsSubscriptionsAsync(2, Ct);
		Assert.Equal(0, live.Current.ContractId);
		Assert.Equal(2, _marketData.ActiveLines);
		Assert.Equal([2, 3], _ibkr.NewsSubscriptions.Select(x => x.Contract.ConId).Order());
		Assert.All(_ibkr.NewsSubscriptions, x => Assert.Null(x.Providers));

		await live.DisposeAsync();
		Assert.Equal(0, _marketData.ActiveLines);
	}

	[Fact]
	public async Task No_broad_tape_among_the_providers_is_an_error()
	{
		await Assert.ThrowsAsync<LSCoreBadRequestException>(async () =>
			await _source
				.SubscribeHeadlinesAsync(
					new SubscribeNewsRequest { Providers = [Provider("BZ")] },
					Ct
				)
				.ToListAsync(Ct)
		);
		Assert.Empty(_ibkr.NewsSubscriptions);
		Assert.Equal(0, _marketData.ActiveLines);
	}

	[Fact]
	public async Task Broad_tapes_are_looked_up_once_per_provider()
	{
		_ibkr.BroadTapes = new()
		{
			["DJNL"] = [new ContractDto { ConId = 3, Symbol = "DJNL:DJNL_ALL" }],
		};

		Assert.True(await _source.HasBroadTapeAsync("DJNL", Ct));
		Assert.False(await _source.HasBroadTapeAsync("BZ", Ct));
		Assert.True(await _source.HasBroadTapeAsync("DJNL", Ct));
		Assert.False(await _source.HasBroadTapeAsync("BZ", Ct));

		Assert.Equal(2, _ibkr.BroadTapeCalls);
	}

	[Theory]
	[InlineData("DJ-N")]
	[InlineData("DJ-RT")]
	[InlineData("DJ-RTA")]
	[InlineData("DJ-RTE")]
	[InlineData("DJ-RTG")]
	public async Task Dow_jones_providers_have_the_shared_tape_without_a_lookup(string code)
	{
		Assert.True(await _source.HasBroadTapeAsync(code, Ct));
		Assert.Equal(0, _ibkr.BroadTapeCalls);
	}

	[Fact]
	public async Task Dow_jones_providers_share_one_subscription_next_to_their_own_tapes()
	{
		_ibkr.BroadTapes = new()
		{
			["BRFG"] =
			[
				new ContractDto
				{
					ConId = 2,
					Symbol = "BRFG:BRFG_ALL",
					SecurityType = "NEWS",
				},
			],
		};
		_ibkr.LiveNews = [Headline("1", "DJ-RT")];
		await using var live = _source
			.SubscribeHeadlinesAsync(
				new SubscribeNewsRequest
				{
					Providers =
					[
						Provider("DJ-N"),
						Provider("BRFG"),
						Provider("DJ-RT"),
						Provider("DJ-RTG"),
					],
				},
				Ct
			)
			.GetAsyncEnumerator(Ct);

		Assert.True(await live.MoveNextAsync());
		await _ibkr.WaitForNewsSubscriptionsAsync(2, Ct);
		Assert.Equal(2, _marketData.ActiveLines);
		Assert.Equal(2, _ibkr.NewsSubscriptions.Count);
		Assert.Single(_ibkr.NewsSubscriptions, x => x.Contract.ConId == 2);
		var dowJones = Assert.Single(_ibkr.NewsSubscriptions, x => x.Contract.Exchange == "DJ");
		Assert.Equal(
			("DJ:N_DJN", "NEWS"),
			(dowJones.Contract.Symbol, dowJones.Contract.SecurityType)
		);
		Assert.Null(dowJones.Providers);
		Assert.Equal(1, _ibkr.BroadTapeCalls); // BRFG only

		await live.DisposeAsync();
		Assert.Equal(0, _marketData.ActiveLines);
	}

	[Fact]
	public async Task The_shared_tape_keeps_only_the_selected_providers_headlines()
	{
		_ibkr.LiveNews =
		[
			Headline("1", "DJ-N"),
			Headline("2", "DJ-RT"),
			Headline("3", "DJ-RTE"),
			Headline("4", "DJ-RTA"),
		];
		await using var live = _source
			.SubscribeHeadlinesAsync(
				new SubscribeNewsRequest { Providers = [Provider("DJ-RT"), Provider("DJ-RTA")] },
				Ct
			)
			.GetAsyncEnumerator(Ct);

		Assert.True(await live.MoveNextAsync());
		var first = live.Current;
		Assert.True(await live.MoveNextAsync());

		Assert.Equal(
			[("DJ-RT", "2"), ("DJ-RTA", "4")],
			[
				(first.ProviderCode, first.ArticleId),
				(live.Current.ProviderCode, live.Current.ArticleId),
			]
		);
		Assert.Single(_ibkr.NewsSubscriptions);
		Assert.Equal(1, _marketData.ActiveLines);
	}

	[Fact]
	public async Task A_live_subscription_is_refused_at_the_line_limit()
	{
		for (var i = 0; i < MarketDataConstants.MaxLines; i++)
			_marketData.AcquireLine();

		await Assert.ThrowsAsync<LSCoreBadRequestException>(async () =>
			await _source
				.SubscribeHeadlinesAsync(
					new SubscribeNewsRequest { Contract = Aapl, Providers = [Provider("DJ-N")] },
					Ct
				)
				.ToListAsync(Ct)
		);
		Assert.Equal(MarketDataConstants.MaxLines, _marketData.ActiveLines);
	}
}
