namespace LT.Contracts.Dtos.News;

/// <summary>What IB's headline metadata (<c>A:800015:L:en:K:-0.97:C:0.97</c>) says.</summary>
public class NewsExtraDataDto
{
	public string? Language { get; set; }
	public double? Sentiment { get; set; }
	public double? Confidence { get; set; }
}
