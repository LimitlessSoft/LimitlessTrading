using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>
/// The watchlist's row order: pinned rows first, in the order they were pinned, whatever the sort;
/// the rest by the sorted column.
/// </summary>
public static partial class WatchlistSortHelpers
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
	)
	{
		var next = NextSort(column, null, direction, clicked, null);
		return (next.Column, next.Direction);
	}

	/// <summary>
	/// <see cref="NextSort(WatchlistSortColumn, ListSortDirection, WatchlistSortColumn)"/> where
	/// custom columns (<see cref="WatchlistSortColumn.Custom"/>) are told apart by their id.
	/// </summary>
	public static (
		WatchlistSortColumn Column,
		string? CustomColumnId,
		ListSortDirection Direction
	) NextSort(
		WatchlistSortColumn column,
		string? customColumnId,
		ListSortDirection direction,
		WatchlistSortColumn clicked,
		string? clickedCustomColumnId
	) =>
		clicked != column || clickedCustomColumnId != customColumnId
			? (clicked, clickedCustomColumnId, ListSortDirection.Ascending)
		: direction == ListSortDirection.Ascending
			? (column, customColumnId, ListSortDirection.Descending)
		: (column, customColumnId, ListSortDirection.Ascending);

	/// <summary>
	/// The sort a saved widget restores (hidden columns included): <c>None</c> for an unknown
	/// column (a newer version's) or a custom column without an id, ascending unless
	/// <paramref name="descending"/>. A custom column that no longer exists is dropped when the
	/// columns load.
	/// </summary>
	public static (
		WatchlistSortColumn Column,
		string? CustomColumnId,
		ListSortDirection Direction
	) ResolveSort(WatchlistSortColumn? column, string? customColumnId, bool? descending)
	{
		var direction =
			descending == true ? ListSortDirection.Descending : ListSortDirection.Ascending;
		return column switch
		{
			WatchlistSortColumn.Custom when !string.IsNullOrEmpty(customColumnId) => (
				WatchlistSortColumn.Custom,
				customColumnId,
				direction
			),
			{ } builtIn when WatchlistColumnHelpers.BuiltInColumns.Contains(builtIn) => (
				builtIn,
				null,
				direction
			),
			_ => (WatchlistSortColumn.None, null, ListSortDirection.Ascending),
		};
	}

	/// <summary>
	/// A custom column cell's sort key; null when blank (sorted last either way). Values that are
	/// numbers (<see cref="TryParseNumber"/>) sort by value and before text; text sorts ignoring
	/// case. Descending reverses both, so text comes first there.
	/// </summary>
	public static IComparable? GetCustomSortKey(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;
		var text = value.Trim();
		return new CustomKey(TryParseNumber(text, out var number) ? number : null, text);
	}

	/// <summary>
	/// A cell's text as a number: invariant culture ("1234.5", "-3", "1e6"), with optional
	/// thousands commas ("1,234.5"), a leading "+" or a trailing "%". "1,5" is text, not 15.
	/// </summary>
	public static bool TryParseNumber(string text, out double value)
	{
		var s = text.Trim();
		if (s.EndsWith('%'))
			s = s[..^1].TrimEnd();
		if (ThousandsPattern().IsMatch(s))
			s = s.Replace(",", string.Empty);
		return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
			&& double.IsFinite(value);
	}

	[GeneratedRegex(@"^[+-]?\d{1,3}(,\d{3})+(\.\d+)?$")]
	private static partial Regex ThousandsPattern();

	// Numbers by value before text; text ignoring case; equal keys tie (saved order)
	private sealed class CustomKey(double? number, string text) : IComparable
	{
		public int CompareTo(object? obj)
		{
			if (obj is not CustomKey other)
				return 1;
			return (number, other.Number) switch
			{
				({ } a, { } b) => a.CompareTo(b),
				(not null, null) => -1,
				(null, not null) => 1,
				_ => StringComparer.OrdinalIgnoreCase.Compare(text, other.Text),
			};
		}

		private double? Number => number;
		private string Text => text;
	}

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
