namespace LT.Contracts.Dtos.News;

/// <summary>A provider of a news source, e.g. Dow Jones (DJ-N) of IB.</summary>
public class NewsProviderDto
{
	/// <summary>The <c>INewsSource.Id</c> it comes from ("ibkr").</summary>
	public string SourceId { get; set; } = string.Empty;
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
}
