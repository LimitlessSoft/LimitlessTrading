using LT.Contracts.Enums;

namespace LT.Contracts.Entities;

/// <summary>
/// An article read once, kept for offline reading: a line of
/// <c>news/articles/&lt;yyyy-MM-dd&gt;.jsonl</c> (the UTC day of its headline).
/// </summary>
public class NewsArticleEntity
{
	/// <summary><c>NewsHelpers.GetKey</c> of its headline.</summary>
	public string Key { get; set; } = string.Empty;
	public NewsArticleKind Kind { get; set; }
	public string Body { get; set; } = string.Empty;
	public string? Url { get; set; }
}
