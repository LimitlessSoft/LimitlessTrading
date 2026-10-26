using LT.Contracts.Dtos.News;
using LT.Contracts.Requests.News;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>Headlines from every registered news source, merged, deduplicated, newest first.</summary>
public interface INewsManager
{
	/// <summary>Providers of every available source; a source that fails is left out.</summary>
	Task<IReadOnlyList<NewsProviderDto>> GetProvidersAsync(
		CancellationToken cancellationToken = default
	);

	/// <summary>Past headlines of a contract, newest first, at most <see cref="GetNewsHeadlinesRequest.Limit"/>.</summary>
	Task<NewsHeadlinesPageDto> GetHeadlinesAsync(
		GetNewsHeadlinesRequest request,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Live headlines (each once) until cancelled. Every live subscription uses a market data line.
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

	/// <summary>The headline's full article; recent ones are served from memory.</summary>
	Task<NewsArticleDto> GetArticleAsync(
		NewsHeadlineDto headline,
		CancellationToken cancellationToken = default
	);
}
