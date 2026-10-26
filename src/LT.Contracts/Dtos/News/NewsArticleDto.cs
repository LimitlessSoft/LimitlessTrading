using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.News;

public class NewsArticleDto
{
	public NewsArticleKind Kind { get; set; }

	/// <summary>Text, HTML, or base64 for <see cref="NewsArticleKind.Binary"/>.</summary>
	public string Body { get; set; } = string.Empty;
}
