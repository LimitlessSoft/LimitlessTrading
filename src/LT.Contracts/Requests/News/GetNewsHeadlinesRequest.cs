using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.News;

namespace LT.Contracts.Requests.News;

/// <summary>Past headlines of a contract, or with no contract of the providers' broad feeds.</summary>
public class GetNewsHeadlinesRequest
{
	/// <summary>
	/// Null: every headline of the providers (broad). IB needs the conId; the app's own feeds and
	/// the archive go by the symbol, so a contract typed while disconnected (symbol only) works.
	/// </summary>
	public ContractDto? Contract { get; set; }

	/// <summary>Of any source; each source is asked for its own.</summary>
	public IReadOnlyList<NewsProviderDto> Providers { get; set; } = [];

	/// <summary>Only headlines strictly older than this; null for the latest.</summary>
	public DateTimeOffset? Before { get; set; }

	public int Limit { get; set; } = NewsConstants.HeadlinesPageSize;
}
