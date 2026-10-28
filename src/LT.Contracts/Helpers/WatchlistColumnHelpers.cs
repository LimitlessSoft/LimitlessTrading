using LT.Contracts.Constants;
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
		WatchlistSortColumn.AverageVolume,
		WatchlistSortColumn.MarketCap,
	];

	/// <summary>
	/// The generic ticks the watchlist's quote lines ask for: the average volume's
	/// (<c>MarketDataConstants.AverageVolumeGenericTick</c>) while its column is shown or sorted
	/// by, none otherwise, so the lines can be shared with the rest of the app.
	/// </summary>
	public static string GetGenericTicks(
		IReadOnlySet<WatchlistSortColumn> hidden,
		WatchlistSortColumn sortColumn
	) =>
		!hidden.Contains(WatchlistSortColumn.AverageVolume)
		|| sortColumn == WatchlistSortColumn.AverageVolume
			? MarketDataConstants.AverageVolumeGenericTick
			: string.Empty;

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
