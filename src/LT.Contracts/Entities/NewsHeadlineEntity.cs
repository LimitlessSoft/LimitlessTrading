namespace LT.Contracts.Entities;

/// <summary>One archived headline: a line of <c>news/&lt;yyyy-MM-dd&gt;.jsonl</c> (UTC day of <see cref="Time"/>).</summary>
public class NewsHeadlineEntity
{
	public string SourceId { get; set; } = string.Empty;
	public string ProviderCode { get; set; } = string.Empty;
	public string ArticleId { get; set; } = string.Empty;
	public DateTimeOffset Time { get; set; }
	public string Headline { get; set; } = string.Empty;
	public string? Summary { get; set; }
	public string? Url { get; set; }

	/// <summary>Tickers it is about, the primary one first.</summary>
	public List<string>? Symbols { get; set; }
	public int ContractId { get; set; }
	public string? Language { get; set; }
	public double? Sentiment { get; set; }
	public double? Confidence { get; set; }
}
