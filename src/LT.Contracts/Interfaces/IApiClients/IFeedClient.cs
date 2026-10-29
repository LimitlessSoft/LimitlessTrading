using LT.Contracts.Dtos.Feeds;

namespace LT.Contracts.Interfaces.IApiClients;

/// <summary>
/// Public RSS / Atom feeds and web pages over HTTPS for the app's own news source (no account, no
/// key). Requests to one host are spaced (<c>FeedNewsConstants.HostRequestSpacing</c>) and carry
/// the contact User-Agent the SEC requires. Throws <see cref="Exceptions.FeedServiceException"/>
/// with a message ready to show.
/// </summary>
public interface IFeedClient
{
	/// <summary>
	/// The feed's items (RSS 2.0 or Atom). With the last answer's <paramref name="conditions"/> an
	/// unchanged feed answers <see cref="FeedResponseDto.NotModified"/> and costs nothing.
	/// </summary>
	Task<FeedResponseDto> GetAsync(
		string url,
		FeedConditionsDto? conditions = null,
		CancellationToken cancellationToken = default
	);

	/// <summary>A page as text (HTML), at most <c>FeedNewsConstants.MaxArticleBytes</c>.</summary>
	Task<string> GetTextAsync(string url, CancellationToken cancellationToken = default);
}
