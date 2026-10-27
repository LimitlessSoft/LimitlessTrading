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
}
