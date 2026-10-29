using LT.Contracts.Constants;
using LT.Contracts.Dtos.News;

namespace LT.Contracts.Requests.News;

/// <summary>Headlines of the local news archive containing every word of <see cref="Query"/>.</summary>
public class SearchNewsRequest
{
	public string Query { get; set; } = string.Empty;

	/// <summary>Of any source, available or not (the archive keeps them all); empty for every provider.</summary>
	public IReadOnlyList<NewsProviderDto> Providers { get; set; } = [];

	/// <summary>Only headlines strictly older than this; null for the latest.</summary>
	public DateTimeOffset? Before { get; set; }

	public int Limit { get; set; } = NewsConstants.HeadlinesPageSize;
}
