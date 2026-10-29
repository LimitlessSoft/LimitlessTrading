using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.News;

/// <summary>A provider of a news source, e.g. Dow Jones (DJ-N) of IB, SEC filings of ours.</summary>
public class NewsProviderDto
{
	/// <summary>The <c>INewsSource.Id</c> it comes from ("ibkr", "lt").</summary>
	public string SourceId { get; set; } = string.Empty;
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// What this provider serves; providers of one source differ (CNBC has no per-symbol news,
	/// Yahoo no broad feed). IB's say everything; whether one has a broad tape is asked separately.
	/// </summary>
	public NewsSourceCapabilities Capabilities { get; set; } = NewsSourceCapabilities.All;

	/// <summary>Selected in a news widget that has no saved choice for its source.</summary>
	public bool SelectedByDefault { get; set; } = true;
}
