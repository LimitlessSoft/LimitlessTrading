namespace LT.Contracts.Enums;

/// <summary>Whether a chart bar has its footprint.</summary>
public enum FootprintBarState
{
	/// <summary>Its trades aren't loaded (yet): drawn as a candle marked as loading.</summary>
	Loading = 0,

	/// <summary>Every trade of the bar is counted.</summary>
	Ready = 1,

	/// <summary>Its trades can't be had (past the backfill, no live stream): a marked candle.</summary>
	Unavailable = 2,
}
