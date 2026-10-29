using LT.Contracts.Constants;
using LT.Contracts.Dtos.News;
using LT.Contracts.Helpers;
using LT.Domain.Managers;
using LT.Domain.NewsSources;
using LT.Tests.Fakes;
using LT.Tests.Feeds;

namespace LT.Tests.Domain.NewsSources;

/// <summary>The app's feed poller on a fake clock, a fake feed client and a temp-folder archive.</summary>
public sealed class NewsFeedPollerTests : IDisposable
{
	private static readonly string PrnUrl = NewsFeedHelpers.FindFeed("PRN")!.BroadUrls[0];
	private static readonly string SecUrl = NewsFeedHelpers.FindFeed("SEC")!.BroadUrls[0];
	private readonly TestNewsArchive _archive = new();
	private readonly FakeFeedClient _feeds = new();
	private readonly FakeSecApiClient _sec = new();
	private readonly MessageLogManager _log = new();
	private readonly NewsFeedPoller _poller;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public NewsFeedPollerTests() =>
		_poller = new NewsFeedPoller(
			_feeds,
			_sec,
			_archive.Manager,
			_log,
			_archive.SettingsManager,
			_archive.Time
		);

	public void Dispose() => _archive.Dispose();

	[Fact]
	public async Task Two_watchers_of_a_feed_cost_one_request_per_interval_and_get_only_new_items()
	{
		_feeds.Feeds[PrnUrl] = FakeFeedClient.Rss(("1", "One", 1, null), ("2", "Two", 2, null));
		using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
		var a = Collect(_poller.WatchAsync("PRN", null, stop.Token));
		await WaitFor(() => a.Count == 2);
		// Joins the running loop: what is archived already is history, not live
		var b = Collect(_poller.WatchAsync("PRN", null, stop.Token));
		await Task.Delay(50, Ct);

		_feeds.Feeds[PrnUrl] = FakeFeedClient.Rss(("3", "Three", 3, null), ("2", "Two", 2, null));
		await AdvanceUntil(() => a.Count == 3 && b.Count == 1, TimeSpan.FromSeconds(60));

		Assert.Equal(2, _feeds.RequestCount(PrnUrl));
		Assert.Equal(["1", "2", "3"], a.Select(x => x.ArticleId).Order());
		Assert.Equal("3", b[0].ArticleId);
		Assert.Equal(3, _archive.Repository.GetHeadlines(new DateOnly(2026, 10, 29)).Count);
		stop.Cancel();
	}

	[Fact]
	public async Task An_unchanged_feed_is_asked_with_its_etag_and_gives_nothing()
	{
		_feeds.Feeds[PrnUrl] = FakeFeedClient.Rss(("1", "One", 1, null));
		_feeds.Unchanged.Add(PrnUrl);
		using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
		var seen = Collect(_poller.WatchAsync("PRN", null, stop.Token));
		await WaitFor(() => seen.Count == 1);

		await AdvanceUntil(() => _feeds.RequestCount(PrnUrl) == 2, TimeSpan.FromSeconds(60));

		Assert.NotNull(_feeds.Requests.Last(x => x.Url == PrnUrl).Conditions?.ETag);
		Assert.Single(seen);
		stop.Cancel();
	}

	[Fact]
	public async Task The_loop_stops_when_the_last_watcher_leaves()
	{
		_feeds.Feeds[PrnUrl] = FakeFeedClient.Rss(("1", "One", 1, null));
		using (var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct))
		{
			var seen = Collect(_poller.WatchAsync("PRN", null, stop.Token));
			await WaitFor(() => seen.Count == 1);
			stop.Cancel();
			await Task.Delay(50, Ct);
		}

		_archive.Time.Advance(TimeSpan.FromMinutes(5));
		await Task.Delay(50, Ct);

		Assert.Equal(1, _feeds.RequestCount(PrnUrl));
	}

	[Fact]
	public async Task A_failing_feed_is_logged_once_and_retried_at_its_interval()
	{
		using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
		_ = Collect(_poller.WatchAsync("PRN", null, stop.Token));
		await WaitFor(() => _feeds.RequestCount(PrnUrl) == 1);
		await AdvanceUntil(() => _feeds.RequestCount(PrnUrl) == 2, TimeSpan.FromSeconds(60));

		var warning = Assert.Single(_log.GetAll(), x => x.Source == "News feeds");
		Assert.Contains("PR Newswire", warning.Text);

		_feeds.Feeds[PrnUrl] = FakeFeedClient.Rss(("1", "One", 1, null));
		await AdvanceUntil(() => _log.GetAll().Count == 2, TimeSpan.FromSeconds(60));
		Assert.Contains("answers again", _log.GetAll()[^1].Text);
		stop.Cancel();
	}

	[Fact]
	public async Task Filings_are_tagged_with_the_filers_tickers()
	{
		_sec.Ciks["SSTK"] = 1549346;
		_feeds.Feeds[SecUrl] = FeedFixtures.Read("sec-current-8k.xml");
		using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);

		var seen = Collect(_poller.WatchAsync("SEC", null, stop.Token));
		await WaitFor(() => seen.Count == 3);

		Assert.Equal("SSTK", Assert.Single(seen, x => x.Symbols.Count > 0).Symbol);
		Assert.Equal(1, _sec.TickerCalls);
		stop.Cancel();
	}

	[Fact]
	public async Task A_symbols_feed_is_fetched_once_for_a_while_and_archived()
	{
		var url = NewsFeedHelpers.BuildSymbolUrl(
			NewsFeedHelpers.FindFeed("YF")!,
			"AAPL",
			null,
			null
		)!;
		_feeds.Feeds[url] = FeedFixtures.Read("yahoo-aapl.xml");

		var first = await _poller.FetchAsync("YF", "AAPL", null, Ct);
		var again = await _poller.FetchAsync("YF", "aapl", null, Ct);
		_archive.Time.Advance(FeedNewsConstants.SymbolFetchTtl);
		await _poller.FetchAsync("YF", "AAPL", null, Ct);

		Assert.Equal(2, first.Headlines.Count);
		Assert.All(first.Headlines, x => Assert.Equal("AAPL", x.Symbol));
		Assert.Empty(again.Headlines);
		Assert.Equal(2, _feeds.RequestCount(url));
		Assert.Equal(2, _archive.Repository.GetHeadlines(new DateOnly(2026, 10, 29)).Count);
	}

	[Fact]
	public async Task A_symbol_the_sec_does_not_know_has_no_filings_feed()
	{
		Assert.Empty((await _poller.FetchAsync("SEC", "ZZZZ", null, Ct)).Headlines);
		Assert.Empty(_feeds.Requests);
	}

	[Fact]
	public async Task Routine_fund_filings_are_left_out_but_a_full_page_still_pages_back()
	{
		_sec.Ciks["SPY"] = 884394;
		var url = NewsFeedHelpers.BuildSymbolUrl(
			NewsFeedHelpers.FindFeed("SEC")!,
			"SPY",
			884394,
			null
		)!;
		_feeds.Feeds[url] = FakeFeedClient.Rss([
			.. Enumerable
				.Range(0, FeedNewsConstants.SecPageSize - 1)
				.Select(i =>
					(
						$"n{i}",
						"NPORT-P - Monthly Portfolio Investments Report",
						i % 60,
						(string?)null
					)
				),
			("k", "8-K - Current report", 0, null),
		]);

		var page = await _poller.FetchAsync("SEC", "SPY", null, Ct);
		var again = await _poller.FetchAsync("SEC", "SPY", null, Ct);

		Assert.Equal(["k"], page.Headlines.Select(x => x.ArticleId));
		Assert.True(page.HasMore);
		Assert.Empty(again.Headlines);
		Assert.True(again.HasMore);
		Assert.Single(_archive.Repository.GetHeadlines(new DateOnly(2026, 10, 29)));
	}

	[Fact]
	public async Task Items_older_than_the_archive_keeps_are_not_archived_again()
	{
		var url = NewsFeedHelpers.BuildSymbolUrl(
			NewsFeedHelpers.FindFeed("YF")!,
			"SPY",
			null,
			null
		)!;
		_feeds.Feeds[url] =
			"<?xml version=\"1.0\"?><rss version=\"2.0\"><channel><title>t</title>"
			+ "<item><title>New</title><guid>new</guid><pubDate>Wed, 28 Oct 2026 12:00:00 GMT</pubDate></item>"
			+ "<item><title>Old</title><guid>old</guid><pubDate>Mon, 28 Aug 2023 12:00:00 GMT</pubDate></item>"
			+ "</channel></rss>";

		var page = await _poller.FetchAsync("YF", "SPY", null, Ct);

		Assert.Equal(["new"], page.Headlines.Select(x => x.ArticleId));
		Assert.Equal([new DateOnly(2026, 10, 28)], _archive.Repository.GetDays());
	}

	private static List<NewsHeadlineDto> Collect(IAsyncEnumerable<NewsHeadlineDto> stream)
	{
		var list = new List<NewsHeadlineDto>();
		_ = Task.Run(async () =>
		{
			try
			{
				await foreach (var headline in stream)
					lock (list)
						list.Add(headline);
			}
			catch (OperationCanceledException) { }
		});
		return list;
	}

	// The loop may not be waiting yet when the clock moves: move it again until it was
	private async Task AdvanceUntil(Func<bool> condition, TimeSpan step)
	{
		for (var i = 0; i < 20 && !condition(); i++)
		{
			_archive.Time.Advance(step);
			for (var j = 0; j < 20 && !condition(); j++)
				await Task.Delay(10, Ct);
		}
		Assert.True(condition());
	}

	private static async Task WaitFor(Func<bool> condition)
	{
		for (var i = 0; i < 300 && !condition(); i++)
			await Task.Delay(10, Ct);
		Assert.True(condition());
	}
}
