using System.Runtime.CompilerServices;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.News;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.INewsSources;
using LT.Contracts.Requests.News;

namespace LT.Domain.NewsSources;

/// <summary>
/// The app's own news ("LimitlessTrading"): public feeds the app polls itself
/// (<see cref="FeedNewsConstants.Feeds"/>, <see cref="INewsFeedPoller"/>), no account, no IB
/// connection. History comes mostly from the archive (the news manager merges it); this source
/// adds a symbol's own feeds fetched once, and pages SEC filings back. Live is polling: a symbol's
/// feeds plus the broad press release / filing feeds filtered to the symbol. Articles are the
/// feed's summary with its link; a SEC filing reads its index and press release exhibit.
/// Available while turned on in Settings (<c>NewsFeeds</c>). Registered as a singleton.
/// </summary>
public class FeedNewsSource(
	INewsFeedPoller poller,
	IFeedClient feedClient,
	ISettingsManager settingsManager
) : INewsSource
{
	public string Id => FeedNewsConstants.SourceId;

	public string Name => FeedNewsConstants.SourceName;

	public NewsSourceCapabilities Capabilities => NewsSourceCapabilities.All;

	public bool IsAvailable => settingsManager.GetAppSettings().NewsFeeds;

	public Task<IReadOnlyList<NewsProviderDto>> GetProvidersAsync(
		CancellationToken cancellationToken = default
	) =>
		Task.FromResult<IReadOnlyList<NewsProviderDto>>([
			.. FeedNewsConstants.Feeds.Select(NewsFeedHelpers.ToProvider),
		]);

	public async Task<NewsHeadlinesPageDto> GetHeadlinesAsync(
		GetNewsHeadlinesRequest request,
		CancellationToken cancellationToken = default
	)
	{
		// Broad history is the archive's (collected in the background)
		if (request.Contract?.Symbol is not { Length: > 0 } symbol)
			return new NewsHeadlinesPageDto();
		var feeds = NewsHelpers
			.GetProviderCodes(request.Providers, Id)
			.Select(NewsFeedHelpers.FindFeed)
			.OfType<NewsFeedDefinitionDto>()
			.Where(x => x.SymbolUrl != null)
			.ToList();
		if (feeds.Count == 0)
			return new NewsHeadlinesPageDto();

		var results = await Task.WhenAll(
			feeds.Select(async feed =>
			{
				try
				{
					return (
						Page: await poller.FetchAsync(
							feed.Code,
							symbol,
							request.Before,
							cancellationToken
						),
						Error: (Exception?)null
					);
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					return (Page: new NewsHeadlinesPageDto(), Error: ex);
				}
			})
		);
		if (results.All(x => x.Error != null))
			throw results[0].Error!;

		var headlines = results
			.SelectMany(x => x.Page.Headlines)
			.Where(x => request.Before == null || x.Time < request.Before)
			.OrderByDescending(x => x.Time)
			.ToList();
		return new NewsHeadlinesPageDto
		{
			Headlines = headlines.Take(request.Limit).ToList(),
			// A full page of filings: older ones are a "Load older" away
			HasMore = headlines.Count > request.Limit || results.Any(x => x.Page.HasMore),
		};
	}

	public async IAsyncEnumerable<NewsHeadlineDto> SubscribeHeadlinesAsync(
		SubscribeNewsRequest request,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var feeds = NewsHelpers
			.GetProviderCodes(request.Providers, Id)
			.Select(NewsFeedHelpers.FindFeed)
			.OfType<NewsFeedDefinitionDto>()
			.ToList();
		var symbol = request.Contract?.Symbol;
		List<IAsyncEnumerable<NewsHeadlineDto>> streams = [];
		foreach (var feed in feeds)
		{
			if (symbol is not { Length: > 0 })
			{
				if (feed.BroadUrls.Count > 0)
					streams.Add(poller.WatchAsync(feed.Code, null, cancellationToken));
				continue;
			}
			if (feed.SymbolUrl != null)
				streams.Add(poller.WatchAsync(feed.Code, symbol, cancellationToken));
			if (feed.TagsSymbols && feed.BroadUrls.Count > 0)
				streams.Add(
					poller
						.WatchAsync(feed.Code, null, cancellationToken)
						.Where(x => NewsFeedHelpers.IsAbout(x, symbol))
				);
		}
		if (streams.Count == 0)
			yield break;

		// One headline object reaches every watcher of a feed: passed on as it is, never changed
		await foreach (
			var headline in AsyncEnumerableHelpers.MergeAsync(streams, cancellationToken)
		)
			yield return headline;
	}

	public Task<bool> HasBroadTapeAsync(
		string providerCode,
		CancellationToken cancellationToken = default
	) => Task.FromResult(NewsFeedHelpers.FindFeed(providerCode)?.BroadUrls.Count > 0);

	public async Task<NewsArticleDto> GetArticleAsync(
		NewsHeadlineDto headline,
		CancellationToken cancellationToken = default
	)
	{
		if (
			NewsFeedHelpers.FindFeed(headline.ProviderCode)?.Kind == NewsFeedKind.Sec
			&& headline.Url is { } url
		)
		{
			var (text, pressRelease) = NewsFeedHelpers.ParseSecFilingIndex(
				await feedClient.GetTextAsync(url, cancellationToken),
				url
			);
			if (pressRelease != null)
			{
				var release = NewsHelpers.HtmlToPlainText(
					await feedClient.GetTextAsync(pressRelease, cancellationToken)
				);
				if (release.Length > 0)
					text = $"{text}\n\nPress release:\n\n{release}";
			}
			return new NewsArticleDto
			{
				Kind = NewsArticleKind.Text,
				Body = text,
				Url = url,
			};
		}
		var body = headline.Summary ?? string.Empty;
		return new NewsArticleDto
		{
			Kind = NewsHelpers.GetTextKind(body),
			Body = body,
			Url = headline.Url,
		};
	}
}
