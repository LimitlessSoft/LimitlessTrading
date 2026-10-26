using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.News;

namespace LT.Contracts.Requests.News;

/// <summary>Past headlines of a contract from the given providers.</summary>
public class GetNewsHeadlinesRequest
{
	public ContractDto Contract { get; set; } = new();

	/// <summary>Of any source; each source is asked for its own.</summary>
	public IReadOnlyList<NewsProviderDto> Providers { get; set; } = [];

	/// <summary>Only headlines strictly older than this; null for the latest.</summary>
	public DateTimeOffset? Before { get; set; }

	public int Limit { get; set; } = NewsConstants.HeadlinesPageSize;
}
