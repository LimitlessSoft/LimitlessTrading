using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>
/// The watchlist's shown and hidden columns. Hiding a column only takes it off the grid: the rows
/// can still be sorted by it.
/// </summary>
public static class WatchlistColumnHelpers
{
	/// <summary>The built-in columns, in grid order; custom columns follow them.</summary>
	public static readonly IReadOnlyList<WatchlistSortColumn> BuiltInColumns =
	[
		WatchlistSortColumn.Symbol,
		WatchlistSortColumn.Last,
		WatchlistSortColumn.ChangePercent,
		WatchlistSortColumn.Bid,
		WatchlistSortColumn.Volume,
		WatchlistSortColumn.MarketCap,
	];

	/// <summary>
	/// The built-in columns a saved widget hides: only built-in ones (not <c>None</c> or
	/// <c>Custom</c>, nor values of a newer version), and never all of them while no custom column
	/// is shown, so the grid keeps a column.
	/// </summary>
	public static HashSet<WatchlistSortColumn> ResolveHidden(
		IEnumerable<WatchlistSortColumn>? hidden,
		int shownCustomColumns
	)
	{
		var result = (hidden ?? []).Where(BuiltInColumns.Contains).ToHashSet();
		if (shownCustomColumns == 0 && result.Count == BuiltInColumns.Count)
			result.Remove(WatchlistSortColumn.Symbol);
		return result;
	}

	/// <summary>
	/// Whether a column's shown / hidden switch can be flipped: a hidden one can always be shown,
	/// a shown one hidden only while another column stays shown.
	/// </summary>
	public static bool CanToggle(bool isShown, int shownColumns) => !isShown || shownColumns > 1;
}
