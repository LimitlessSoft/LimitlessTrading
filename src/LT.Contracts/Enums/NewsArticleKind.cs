namespace LT.Contracts.Enums;

/// <summary>How a news article's body is encoded.</summary>
public enum NewsArticleKind
{
	Text = 0,
	Html = 1,

	/// <summary>A document (e.g. PDF), base64-encoded.</summary>
	Binary = 2,
}
