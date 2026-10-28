namespace LT.Contracts.Enums;

/// <summary>The watchlist column its unpinned rows are sorted by; <c>None</c> keeps the saved order.</summary>
public enum WatchlistSortColumn
{
	None,
	Symbol,
	Last,
	ChangePercent,
	Bid,
	Volume,
	MarketCap,

	/// <summary>A custom column; which one is kept beside the sort (its id).</summary>
	Custom,

	/// <summary>IB's 90-day average daily volume (generic tick 165).</summary>
	AverageVolume,

	/// <summary>IB's volume rate: shares traded per minute right now (generic tick 295).</summary>
	VolumeRate,

	/// <summary>
	/// The average daily volume spread over a regular session, in shares per minute, to compare
	/// with <see cref="VolumeRate"/>.
	/// </summary>
	AverageVolumeRate,
}
