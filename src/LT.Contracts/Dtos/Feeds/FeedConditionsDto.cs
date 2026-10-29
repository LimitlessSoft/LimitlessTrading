namespace LT.Contracts.Dtos.Feeds;

/// <summary>The validators of the last answer, sent back as If-None-Match / If-Modified-Since.</summary>
public class FeedConditionsDto
{
	public string? ETag { get; set; }
	public DateTimeOffset? LastModified { get; set; }
}
