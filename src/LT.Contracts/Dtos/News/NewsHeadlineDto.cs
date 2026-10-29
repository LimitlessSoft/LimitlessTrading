namespace LT.Contracts.Dtos.News;

/// <summary>One headline. (SourceId, ProviderCode, ArticleId) identifies it across history and live.</summary>
public class NewsHeadlineDto
{
	public string SourceId { get; set; } = string.Empty;
	public string ProviderCode { get; set; } = string.Empty;
	public string ArticleId { get; set; } = string.Empty;

	/// <summary>UTC.</summary>
	public DateTimeOffset Time { get; set; }
	public string Headline { get; set; } = string.Empty;

	/// <summary>Language code (en, de …) when the provider says.</summary>
	public string? Language { get; set; }

	/// <summary>-1 … 1 when the provider scores it.</summary>
	public double? Sentiment { get; set; }

	/// <summary>0 … 1, how sure the sentiment score is.</summary>
	public double? Confidence { get; set; }

	/// <summary>The contract it was asked for; 0 for broad tape headlines.</summary>
	public int ContractId { get; set; }

	/// <summary>
	/// Ticker it was asked for, or the first ticker a broad headline was tagged with (a press
	/// release's <c>(NYSE: BAC)</c>, a filing's filer); empty when it is about none.
	/// </summary>
	public string Symbol { get; set; } = string.Empty;

	/// <summary>Every ticker it is about (<see cref="Symbol"/> first); empty when none.</summary>
	public IReadOnlyList<string> Symbols { get; set; } = [];

	/// <summary>The feed's short description (plain text or HTML) when it gives one.</summary>
	public string? Summary { get; set; }

	/// <summary>The story on the web, when the source links it.</summary>
	public string? Url { get; set; }
}
