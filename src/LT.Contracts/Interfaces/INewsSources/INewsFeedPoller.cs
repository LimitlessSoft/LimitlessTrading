using LT.Contracts.Dtos.News;

namespace LT.Contracts.Interfaces.INewsSources;

/// <summary>
/// Polls the app's own feeds (<c>FeedNewsConstants.Feeds</c>): one loop per feed URL, shared by
/// everyone watching it (three widgets on SPY cost one request per interval), stopped when the
/// last watcher leaves. Items older than the archive's retention are dropped; every other item is
/// archived (<c>INewsArchiveManager</c>); watchers get only the
/// ones not archived before. A failing feed is retried at its interval and reported once in the
/// message log. Singleton.
/// </summary>
public interface INewsFeedPoller
{
	/// <summary>
	/// New headlines of the provider's broad feeds (<paramref name="symbol"/> null) or of its feed
	/// for one symbol, until cancelled. Nothing when it has no such feed.
	/// </summary>
	IAsyncEnumerable<NewsHeadlineDto> WatchAsync(
		string providerCode,
		string? symbol,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// The provider's feed for <paramref name="symbol"/> now (archived), for history; asked at most
	/// once per <c>FeedNewsConstants.SymbolFetchTtl</c> per URL (empty in between, the archive has
	/// them). SEC pages back to filings before <paramref name="before"/>;
	/// <see cref="NewsHeadlinesPageDto.HasMore"/> when its page was full (before routine forms
	/// were left out).
	/// </summary>
	Task<NewsHeadlinesPageDto> FetchAsync(
		string providerCode,
		string symbol,
		DateTimeOffset? before,
		CancellationToken cancellationToken = default
	);
}
