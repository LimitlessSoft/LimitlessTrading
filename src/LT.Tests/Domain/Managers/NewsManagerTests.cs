using LSCore.Exceptions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;
using LT.Contracts.Requests.News;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.Managers;

public class NewsManagerTests
{
	private static readonly ContractDto Aapl = new() { ConId = 265598, Symbol = "AAPL" };
	private readonly FakeNewsSource _ibkr = new("ibkr");
	private readonly FakeNewsSource _rss = new("rss");
	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero)
	);

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public NewsManagerTests() => LSCoreContainer.EnsureInitialized();

	private NewsManager Manager() => new([_ibkr, _rss], _time);

	[Fact]
	public async Task Providers_come_from_every_available_source()
	{
		_ibkr.Providers = [_ibkr.Provider("BRFG"), _ibkr.Provider("DJ-N")];
		_rss.Providers = [_rss.Provider("Reuters")];

		var providers = await Manager().GetProvidersAsync(Ct);

		Assert.Equal(["BRFG", "DJ-N", "Reuters"], providers.Select(x => x.Code));
	}

	[Fact]
	public async Task A_failing_or_unavailable_source_does_not_hide_the_others()
	{
		_ibkr.Providers = [_ibkr.Provider("BRFG")];
		_rss.Error = new InvalidOperationException("feed down");

		Assert.Equal(["BRFG"], (await Manager().GetProvidersAsync(Ct)).Select(x => x.Code));

		_rss.Error = null;
		_rss.Providers = [_rss.Provider("Reuters")];
		_ibkr.IsAvailable = false;
		Assert.Equal(["Reuters"], (await Manager().GetProvidersAsync(Ct)).Select(x => x.Code));
	}

	[Fact]
	public async Task When_every_source_fails_the_error_reaches_the_caller()
	{
		_ibkr.Error = new InvalidOperationException("not connected");
		_rss.Error = new InvalidOperationException("feed down");

		await Assert.ThrowsAsync<InvalidOperationException>(() => Manager().GetProvidersAsync(Ct));
	}

	[Fact]
	public async Task Headlines_are_merged_newest_first_once_each_within_the_limit()
	{
		_ibkr.History = [_ibkr.Headline("DJ-N", "3", 3), _ibkr.Headline("DJ-N", "1", 1)];
		_rss.History =
		[
			_rss.Headline("Reuters", "4", 4),
			_rss.Headline("Reuters", "2", 2),
			_rss.Headline("Reuters", "2", 2),
		];

		var page = await Manager()
			.GetHeadlinesAsync(
				new GetNewsHeadlinesRequest
				{
					Contract = Aapl,
					Providers = [_ibkr.Provider("DJ-N"), _rss.Provider("Reuters")],
					Limit = 3,
				},
				Ct
			);

		Assert.Equal(["4", "3", "2"], page.Headlines.Select(x => x.ArticleId));
		Assert.True(page.HasMore);
	}

	[Fact]
	public async Task Only_sources_with_selected_providers_are_asked()
	{
		_ibkr.History = [_ibkr.Headline("DJ-N", "1", 1)];

		var page = await Manager()
			.GetHeadlinesAsync(
				new GetNewsHeadlinesRequest
				{
					Contract = Aapl,
					Providers = [_ibkr.Provider("DJ-N")],
				},
				Ct
			);

		Assert.Single(page.Headlines);
		Assert.Single(_ibkr.HeadlineRequests);
		Assert.Empty(_rss.HeadlineRequests);
		Assert.False(page.HasMore);
	}

	[Fact]
	public async Task A_source_without_history_is_skipped()
	{
		_rss.Capabilities = NewsSourceCapabilities.Live | NewsSourceCapabilities.BroadTape;

		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			Manager()
				.GetHeadlinesAsync(
					new GetNewsHeadlinesRequest
					{
						Contract = Aapl,
						Providers = [_rss.Provider("Reuters")],
					},
					Ct
				)
		);
	}

	[Theory]
	[InlineData(0, 1)]
	[InlineData(NewsConstants.MaxHeadlinesPerRequest + 1, 265598)]
	[InlineData(10, 0)]
	public async Task Headline_requests_are_validated(int limit, int conId)
	{
		var request = new GetNewsHeadlinesRequest
		{
			Contract = new ContractDto { ConId = conId },
			Providers = [_ibkr.Provider("DJ-N")],
			Limit = limit,
		};

		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			Manager().GetHeadlinesAsync(request, Ct)
		);
		Assert.Empty(_ibkr.HeadlineRequests);
	}

	[Fact]
	public async Task A_request_without_providers_is_refused()
	{
		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			Manager().GetHeadlinesAsync(new GetNewsHeadlinesRequest { Contract = Aapl }, Ct)
		);
		await Assert.ThrowsAsync<LSCoreBadRequestException>(async () =>
			await Manager().SubscribeHeadlinesAsync(new SubscribeNewsRequest(), Ct).ToListAsync(Ct)
		);
	}

	[Fact]
	public async Task Live_headlines_of_every_source_arrive_once_each()
	{
		_ibkr.Live = [_ibkr.Headline("BRFG", "1", 1), _ibkr.Headline("BRFG", "1", 1)];
		_rss.Live = [_rss.Headline("Reuters", "1", 1)];

		var live = await Manager()
			.SubscribeHeadlinesAsync(
				new SubscribeNewsRequest
				{
					Providers = [_ibkr.Provider("BRFG"), _rss.Provider("Reuters")],
				},
				Ct
			)
			.ToListAsync(Ct);

		Assert.Equal(2, live.Count);
		Assert.Single(_ibkr.Subscriptions);
		Assert.Single(_rss.Subscriptions);
	}

	[Fact]
	public async Task Broad_tape_subscriptions_skip_sources_without_one()
	{
		_rss.Capabilities = NewsSourceCapabilities.Live | NewsSourceCapabilities.BySymbol;
		_ibkr.Live = [_ibkr.Headline("BRFG", "1", 1)];

		await Manager()
			.SubscribeHeadlinesAsync(
				new SubscribeNewsRequest
				{
					Providers = [_ibkr.Provider("BRFG"), _rss.Provider("Reuters")],
				},
				Ct
			)
			.ToListAsync(Ct);

		Assert.Single(_ibkr.Subscriptions);
		Assert.Empty(_rss.Subscriptions);
	}

	[Fact]
	public async Task Broad_tape_providers_are_those_that_have_one()
	{
		_ibkr.BroadTapes = ["BRFG", "DJNL"];

		var providers = await Manager()
			.GetBroadTapeProvidersAsync(
				[_ibkr.Provider("BRFG"), _ibkr.Provider("DJ-N"), _ibkr.Provider("DJNL")],
				Ct
			);

		Assert.Equal(["BRFG", "DJNL"], providers.Select(x => x.Code));
	}

	[Fact]
	public async Task Articles_are_kept_for_a_while()
	{
		var manager = Manager();
		var headline = _ibkr.Headline("DJ-N", "1", 1);

		var first = await manager.GetArticleAsync(headline, Ct);
		await manager.GetArticleAsync(headline, Ct);
		Assert.Equal(1, _ibkr.ArticleCalls);
		Assert.Equal("ibkr 1", first.Body);

		_time.Advance(NewsConstants.ArticleCacheTtl);
		await manager.GetArticleAsync(headline, Ct);
		Assert.Equal(2, _ibkr.ArticleCalls);
	}

	[Fact]
	public async Task The_article_cache_drops_the_oldest_past_its_size()
	{
		var manager = Manager();
		for (var i = 0; i <= NewsConstants.ArticleCacheSize; i++)
		{
			await manager.GetArticleAsync(_ibkr.Headline("DJ-N", i.ToString(), 1), Ct);
			_time.Advance(TimeSpan.FromSeconds(1));
		}

		await manager.GetArticleAsync(_ibkr.Headline("DJ-N", "1", 1), Ct);
		Assert.Equal(NewsConstants.ArticleCacheSize + 1, _ibkr.ArticleCalls);
		await manager.GetArticleAsync(_ibkr.Headline("DJ-N", "0", 1), Ct);
		Assert.Equal(NewsConstants.ArticleCacheSize + 2, _ibkr.ArticleCalls);
	}

	[Fact]
	public async Task Articles_of_an_unknown_source_are_refused() =>
		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			Manager().GetArticleAsync(new FakeNewsSource("other").Headline("X", "1", 1), Ct)
		);
}
