using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.News;

/// <summary>One provider of the app's own news source: where its feeds are and how often to ask.</summary>
public class NewsFeedDefinitionDto
{
	/// <summary>The chip's label and the provider code ("SEC", "PRN").</summary>
	public string Code { get; init; } = string.Empty;
	public string Name { get; init; } = string.Empty;
	public NewsFeedKind Kind { get; init; }

	/// <summary>Feeds not tied to a symbol; empty when it has none.</summary>
	public IReadOnlyList<string> BroadUrls { get; init; } = [];

	/// <summary>
	/// A symbol's feed: <c>{0}</c> the ticker (BRK-B style), for SEC <c>{0}</c> the CIK and
	/// <c>{1}</c> a <c>yyyyMMdd</c> date to page back from (empty for the latest); null when none.
	/// </summary>
	public string? SymbolUrl { get; init; }

	/// <summary>Its broad items name the companies they are about (press releases, filings).</summary>
	public bool TagsSymbols { get; init; }

	public TimeSpan PollInterval { get; init; }

	/// <summary>On without the user picking it (official, free to use); else only when picked.</summary>
	public bool SelectedByDefault { get; init; }
}
