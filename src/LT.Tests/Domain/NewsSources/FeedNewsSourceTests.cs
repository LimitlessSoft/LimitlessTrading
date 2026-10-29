using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.News;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.News;
using LT.Contracts.Requests.Settings;
using LT.Domain.Managers;
using LT.Domain.NewsSources;
using LT.Tests.Fakes;
using LT.Tests.Feeds;

namespace LT.Tests.Domain.NewsSources;

public sealed class FeedNewsSourceTests : IDisposable
{
	private static readonly ContractDto Aapl = new() { Symbol = "AAPL", SecurityType = "STK" };
	private readonly TestNewsArchive _archive = new();
	private readonly FakeFeedClient _feeds = new();
	private readonly FakeSecApiClient _sec = new();
	private readonly FeedNewsSource _source;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public FeedNewsSourceTests()
	{
		var poller = new NewsFeedPoller(
			_feeds,
			_sec,
			_archive.Manager,
			new MessageLogManager(),
			_archive.SettingsManager,
			_archive.Time
		);
		_source = new FeedNewsSource(poller, _feeds, _archive.SettingsManager);
	}

	public void Dispose() => _archive.Dispose();

	private async Task<List<NewsProviderDto>> Providers(params string[] codes) =>
		[.. (await _source.GetProvidersAsync(Ct)).Where(x => codes.Contains(x.Code))];

	[Fact]
	public async Task Its_providers_are_the_feeds_named_by_their_chips_with_their_own_capabilities()
	{
		var providers = await _source.GetProvidersAsync(Ct);

		Assert.Equal(FeedNewsConstants.Feeds.Select(x => x.Code), providers.Select(x => x.Code));
		Assert.All(providers, x => Assert.Equal("lt", x.SourceId));
		Assert.False(providers.Single(x => x.Code == "CNBC").SelectedByDefault);
		Assert.Equal("LimitlessTrading", _source.Name);
		Assert.True(await _source.HasBroadTapeAsync("FED", Ct));
		Assert.False(await _source.HasBroadTapeAsync("YF", Ct));
	}

	[Fact]
	public void It_is_available_without_ib_until_turned_off_in_settings()
	{
		Assert.True(_source.IsAvailable);

		var settings = _archive.SettingsManager.GetAppSettings();
		_archive.SettingsManager.SaveAppSettings(
			new SaveAppSettingsRequest
			{
				MarketDataType = settings.MarketDataType,
				HeartbeatSeconds = settings.HeartbeatSeconds,
				NewsFeeds = false,
			}
		);

		Assert.False(_source.IsAvailable);
	}

	[Fact]
	public async Task A_symbols_history_fetches_its_own_feeds_by_ticker()
	{
		var yahoo = NewsFeedHelpers.BuildSymbolUrl(
			NewsFeedHelpers.FindFeed("YF")!,
			"AAPL",
			null,
			null
		)!;
		_feeds.Feeds[yahoo] = FeedFixtures.Read("yahoo-aapl.xml");

		var page = await _source.GetHeadlinesAsync(
			new GetNewsHeadlinesRequest
			{
				Contract = Aapl,
				Providers = await Providers("YF", "PRN"),
			},
			Ct
		);

		Assert.Equal(2, page.Headlines.Count);
		Assert.All(page.Headlines, x => Assert.Equal("YF", x.ProviderCode));
		Assert.Equal([yahoo], _feeds.Requests.Select(x => x.Url));
	}

	[Fact]
	public async Task A_full_page_of_filings_says_older_ones_exist()
	{
		_sec.Ciks["AAPL"] = 320193;
		var url = NewsFeedHelpers.BuildSymbolUrl(
			NewsFeedHelpers.FindFeed("SEC")!,
			"AAPL",
			320193,
			null
		)!;
		_feeds.Feeds[url] = FakeFeedClient.Rss([
			.. Enumerable
				.Range(0, FeedNewsConstants.SecPageSize)
				.Select(i => ($"{i}", $"4 - Filing {i}", i % 60, (string?)null)),
		]);

		var page = await _source.GetHeadlinesAsync(
			new GetNewsHeadlinesRequest { Contract = Aapl, Providers = await Providers("SEC") },
			Ct
		);

		Assert.True(page.HasMore);
	}

	[Fact]
	public async Task Linked_live_news_takes_press_releases_naming_the_symbol()
	{
		var prn = NewsFeedHelpers.FindFeed("PRN")!.BroadUrls[0];
		_feeds.Feeds[prn] = FakeFeedClient.Rss(
			(
				"1",
				"Kimberly-Clark closes offering",
				1,
				"Kimberly-Clark Corporation (NASDAQ: KMB) announced"
			),
			("2", "Millrose sued", 2, "(NYSE: MRP)")
		);
		using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
		var seen = new List<NewsHeadlineDto>();

		var reading = Task.Run(
			async () =>
			{
				await foreach (
					var headline in _source.SubscribeHeadlinesAsync(
						new SubscribeNewsRequest
						{
							Contract = new ContractDto { Symbol = "KMB" },
							Providers = await Providers("PRN"),
						},
						stop.Token
					)
				)
					lock (seen)
						seen.Add(headline);
			},
			Ct
		);
		for (var i = 0; i < 200 && seen.Count == 0; i++)
			await Task.Delay(10, Ct);
		await Task.Delay(50, Ct);
		stop.Cancel();

		Assert.Equal("1", Assert.Single(seen).ArticleId);
	}

	[Fact]
	public async Task A_feed_article_is_its_summary_with_the_link()
	{
		var article = await _source.GetArticleAsync(
			new NewsHeadlineDto
			{
				SourceId = "lt",
				ProviderCode = "PRN",
				ArticleId = "1",
				Summary = "<p>DALLAS -- Kimberly-Clark</p>",
				Url = "https://www.prnewswire.com/news-releases/x.html",
			},
			Ct
		);

		Assert.Equal(NewsArticleKind.Html, article.Kind);
		Assert.Equal("https://www.prnewswire.com/news-releases/x.html", article.Url);
		Assert.Empty(_feeds.Requests);
	}

	[Fact]
	public async Task A_filings_article_is_its_index_and_press_release()
	{
		const string index = "https://www.sec.gov/Archives/edgar/data/1/000/0001-index.htm";
		_feeds.Texts[index] =
			"<div id=\"formName\"><strong>Form 8-K</strong> - Current report:</div>"
			+ "<div class=\"infoHead\">Items</div><div class=\"info\">Item 2.02: Results of Operations<br /></div>"
			+ "<table><tr><td>1</td><td>PRESS RELEASE</td><td><a href=\"/Archives/edgar/data/1/000/ex99.htm\">ex99.htm</a></td><td>EX-99.1</td><td>1</td></tr></table>";
		_feeds.Texts["https://www.sec.gov/Archives/edgar/data/1/000/ex99.htm"] =
			"<html><body><p>Revenue rose 12%.</p></body></html>";

		var article = await _source.GetArticleAsync(
			new NewsHeadlineDto
			{
				SourceId = "lt",
				ProviderCode = "SEC",
				ArticleId = "1",
				Url = index,
			},
			Ct
		);

		Assert.Contains("Item 2.02: Results of Operations", article.Body);
		Assert.Contains("Press release:", article.Body);
		Assert.Contains("Revenue rose 12%.", article.Body);
		Assert.Equal(index, article.Url);
	}
}
