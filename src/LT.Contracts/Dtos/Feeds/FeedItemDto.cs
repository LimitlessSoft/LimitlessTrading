namespace LT.Contracts.Dtos.Feeds;

/// <summary>One item of an RSS or Atom feed, as the feed gives it.</summary>
public class FeedItemDto
{
	/// <summary>The item's guid / id, else its link; stable across polls.</summary>
	public string Id { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;

	/// <summary>Description / summary / content: plain text or HTML.</summary>
	public string? Summary { get; set; }
	public string? Url { get; set; }

	/// <summary>Published (else updated) time; null when the feed has none it can read.</summary>
	public DateTimeOffset? Time { get; set; }

	/// <summary>Categories / Atom category terms (EDGAR: the form type).</summary>
	public IReadOnlyList<string> Categories { get; set; } = [];

	/// <summary>Tickers the feed itself tags the item with (Seeking Alpha's <c>sa:stock</c>).</summary>
	public IReadOnlyList<string> Symbols { get; set; } = [];
}
