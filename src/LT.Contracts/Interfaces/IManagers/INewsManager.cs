using LT.Contracts.Dtos.News;
using LT.Contracts.Requests.News;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>
/// Headlines from every registered news source and the local archive, merged, deduplicated (by key
/// and by story, <see cref="Constants.NewsConstants.StoryWindow"/>), newest first. Every headline
/// passed on is archived.
/// </summary>
public interface INewsManager
{
	/// <summary>Providers of every available source (none: empty); a source that fails is left out.</summary>
	Task<IReadOnlyList<NewsProviderDto>> GetProvidersAsync(
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Past headlines of a contract (or of the providers' broad feeds), newest first, at most
	/// <see cref="GetNewsHeadlinesRequest.Limit"/>: the available sources' merged with the archive,
	/// so providers of an unavailable source still show what was archived.
	/// </summary>
	Task<NewsHeadlinesPageDto> GetHeadlinesAsync(
		GetNewsHeadlinesRequest request,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Live headlines (each once) until cancelled. Every IB subscription uses a market data line.
	/// Without a contract, only providers with a broad tape are subscribed.
	/// </summary>
	IAsyncEnumerable<NewsHeadlineDto> SubscribeHeadlinesAsync(
		SubscribeNewsRequest request,
		CancellationToken cancellationToken = default
	);

	/// <summary>Those of <paramref name="providers"/> that have a broad tape.</summary>
	Task<IReadOnlyList<NewsProviderDto>> GetBroadTapeProvidersAsync(
		IEnumerable<NewsProviderDto> providers,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// The headline's full article; recent ones are served from memory, ones read before from the
	/// archive (also without the source).
	/// </summary>
	Task<NewsArticleDto> GetArticleAsync(
		NewsHeadlineDto headline,
		CancellationToken cancellationToken = default
	);

	/// <summary>Archived headlines with every word of the query, newest first.</summary>
	Task<NewsHeadlinesPageDto> SearchArchiveAsync(
		SearchNewsRequest request,
		CancellationToken cancellationToken = default
	);

	/// <summary>Bytes the local news archive takes.</summary>
	long GetArchiveSizeBytes();

	/// <summary>Deletes every archived headline and article.</summary>
	void ClearArchive();
}
