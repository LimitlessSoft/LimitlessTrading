namespace LT.Contracts.Dtos.Feeds;

/// <summary>A feed's answer, with what to send next time so an unchanged feed costs a 304.</summary>
public class FeedResponseDto
{
	/// <summary>The server answered 304: nothing changed since <see cref="FeedConditionsDto"/>.</summary>
	public bool NotModified { get; set; }

	/// <summary>The feed's own title (EDGAR company feeds: the company's name).</summary>
	public string? Title { get; set; }

	public IReadOnlyList<FeedItemDto> Items { get; set; } = [];

	public FeedConditionsDto Conditions { get; set; } = new();
}
