namespace LT.Contracts.Enums;

/// <summary>Whether a chart bar has its footprint.</summary>
public enum FootprintBarState
{
	/// <summary>None of its trades are loaded yet: drawn as a candle marked as loading.</summary>
	Loading = 0,

	/// <summary>Every trade of the bar is counted.</summary>
	Ready = 1,

	/// <summary>Its trades can't be had (past the backfill, no live stream): a marked candle.</summary>
	Unavailable = 2,

	/// <summary>
	/// Some of its trades are counted (paged back from its end, or the forming bar's live ones);
	/// drawn with them and marked partial until the rest are in.
	/// </summary>
	Partial = 3,
}
