using LT.Contracts.Dtos.News;
using LT.Contracts.Enums;
using LT.Contracts.Requests.News;

namespace LT.Contracts.Interfaces.INewsSources;

/// <summary>
/// Somewhere headlines come from (IB; the app's own feeds, "lt"). Every registered source is
/// aggregated by <c>INewsManager</c>; requests reach a source with only its own providers.
/// </summary>
public interface INewsSource
{
	/// <summary>Stable id saved with provider selections ("ibkr"); a string, so a new source needs no enum value.</summary>
	string Id { get; }

	string Name { get; }

	NewsSourceCapabilities Capabilities { get; }

	/// <summary>Whether it can be asked now (IB: connected; the app's feeds: turned on in Settings).</summary>
	bool IsAvailable { get; }

	Task<IReadOnlyList<NewsProviderDto>> GetProvidersAsync(
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Past headlines, newest first; <see cref="GetNewsHeadlinesRequest.Before"/> pages back.
	/// Requests reach it only with providers that can serve them (their capabilities).
	/// </summary>
	Task<NewsHeadlinesPageDto> GetHeadlinesAsync(
		GetNewsHeadlinesRequest request,
		CancellationToken cancellationToken = default
	);

	/// <summary>Live headlines of the contract, or of the providers' broad tapes, until cancelled.</summary>
	IAsyncEnumerable<NewsHeadlineDto> SubscribeHeadlinesAsync(
		SubscribeNewsRequest request,
		CancellationToken cancellationToken = default
	);

	/// <summary>Whether the provider has a feed not tied to a contract.</summary>
	Task<bool> HasBroadTapeAsync(
		string providerCode,
		CancellationToken cancellationToken = default
	);

	Task<NewsArticleDto> GetArticleAsync(
		NewsHeadlineDto headline,
		CancellationToken cancellationToken = default
	);
}
