using LT.Contracts.Dtos.News;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>
/// Which headlines are "matches" (settings <c>NewsAlertKeywords</c>, <c>NewsAlertWatchlist</c>):
/// highlighted in news widgets, the only ones shown with "Matches only", and a sound
/// (<c>NewsAlertSound</c>) when a new one arrives. Singleton: keeps the rules and watchlist
/// symbols in memory.
/// </summary>
public interface INewsAlertManager
{
	/// <summary>The keywords or the watchlists changed: matches are to be worked out again.</summary>
	event Action? RulesChanged;

	/// <summary>True when there is any rule (keywords, or watchlist symbols when that is on).</summary>
	bool HasRules { get; }

	bool Matches(NewsHeadlineDto headline);

	/// <summary>
	/// New headlines (archived for the first time): plays the sound once when one of them matches
	/// and is recent (<c>NewsConstants.AlertMaxAge</c>). Returns how many matched; never throws.
	/// </summary>
	int OnHeadlines(IReadOnlyList<NewsHeadlineDto> headlines);
}
