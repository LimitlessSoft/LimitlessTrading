using System.ComponentModel;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>
/// The watchlist's row order: pinned rows first, in the order they were pinned, whatever the sort;
/// the rest by the sorted column.
/// </summary>
public static class WatchlistSortHelpers
{
	/// <summary>
	/// <paramref name="rows"/> (in saved order) in display order. Pinned rows (a pin index) come
	/// first by that index. The others follow by <paramref name="getSortKey"/> for
	/// <paramref name="column"/> in <paramref name="direction"/>; rows without a value yet (no quote)
	/// go last either way, and ties, like <see cref="WatchlistSortColumn.None"/>, keep the saved
	/// order.
	/// </summary>
	public static List<T> Order<T>(
		IReadOnlyList<T> rows,
		Func<T, int?> getPinIndex,
		WatchlistSortColumn column,
		ListSortDirection direction,
		Func<T, WatchlistSortColumn, IComparable?> getSortKey
	)
	{
		var pinned = rows.Select(row => (Row: row, Pin: getPinIndex(row)))
			.Where(x => x.Pin is not null)
			.OrderBy(x => x.Pin)
			.Select(x => x.Row);
		var unpinned = rows.Where(row => getPinIndex(row) is null).ToList();
		if (column == WatchlistSortColumn.None)
			return [.. pinned, .. unpinned];

		var keyed = unpinned.Select(row => (Row: row, Key: getSortKey(row, column))).ToList();
		var withKey = keyed.Where(x => x.Key is not null);
		var sorted =
			direction == ListSortDirection.Descending
				? withKey.OrderByDescending(x => x.Key, KeyComparer)
				: withKey.OrderBy(x => x.Key, KeyComparer);
		return
		[
			.. pinned,
			.. sorted.Select(x => x.Row),
			.. keyed.Where(x => x.Key is null).Select(x => x.Row),
		];
	}

	/// <summary>
	/// The sort after clicking <paramref name="clicked"/>'s header: another column starts
	/// ascending, the sorted one flips direction.
	/// </summary>
	public static (WatchlistSortColumn Column, ListSortDirection Direction) NextSort(
		WatchlistSortColumn column,
		ListSortDirection direction,
		WatchlistSortColumn clicked
	) =>
		clicked != column ? (clicked, ListSortDirection.Ascending)
		: direction == ListSortDirection.Ascending ? (column, ListSortDirection.Descending)
		: (column, ListSortDirection.Ascending);

	/// <summary>Whether the column's order changes with quotes, so rows are re-sorted as they tick.</summary>
	public static bool DependsOnQuotes(WatchlistSortColumn column) =>
		column
			is WatchlistSortColumn.Last
				or WatchlistSortColumn.ChangePercent
				or WatchlistSortColumn.Bid
				or WatchlistSortColumn.Volume
				or WatchlistSortColumn.MarketCap;

	// Symbols case-insensitively; numbers by value
	private static readonly IComparer<IComparable?> KeyComparer = Comparer<IComparable?>.Create(
		(a, b) =>
			a is string x && b is string y
				? StringComparer.OrdinalIgnoreCase.Compare(x, y)
				: a!.CompareTo(b)
	);
}
