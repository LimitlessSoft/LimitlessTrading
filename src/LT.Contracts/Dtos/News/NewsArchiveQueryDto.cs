namespace LT.Contracts.Dtos.News;

/// <summary>What to read from the local news archive, newest first.</summary>
public class NewsArchiveQueryDto
{
	/// <summary>Provider keys ("ibkr:BRFG", <c>NewsHelpers.GetProviderKey</c>) to keep; null for every provider.</summary>
	public IReadOnlySet<string>? ProviderKeys { get; init; }

	/// <summary>Only headlines about this ticker; null for all.</summary>
	public string? Symbol { get; init; }

	/// <summary>Only headlines with every one of these words (headline, summary, symbols); null for all.</summary>
	public string? Text { get; init; }

	/// <summary>Only headlines strictly older than this; null for the latest.</summary>
	public DateTimeOffset? Before { get; init; }

	public int Limit { get; init; }
}
