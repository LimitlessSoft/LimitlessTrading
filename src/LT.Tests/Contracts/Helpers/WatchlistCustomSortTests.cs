using System.ComponentModel;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

/// <summary>
/// Sorting by a custom watchlist column: numbers by value before text, text ignoring case, blank
/// cells last in either direction, ties in saved order.
/// </summary>
public class WatchlistCustomSortTests
{
	private sealed record Row(string Symbol, string? Value, int? Pin = null);

	private static string[] Order(
		Row[] rows,
		ListSortDirection direction = ListSortDirection.Ascending
	) =>
		WatchlistSortHelpers
			.Order(
				rows,
				x => x.Pin,
				WatchlistSortColumn.Custom,
				direction,
				(row, _) => WatchlistSortHelpers.GetCustomSortKey(row.Value)
			)
			.Select(x => x.Symbol)
			.ToArray();

	[Fact]
	public void Numbers_sort_by_value_not_as_text()
	{
		Row[] rows = [new("A", "10"), new("B", "9"), new("C", "-2.5"), new("D", "100")];

		Assert.Equal(["C", "B", "A", "D"], Order(rows));
		Assert.Equal(["D", "A", "B", "C"], Order(rows, ListSortDirection.Descending));
	}

	[Fact]
	public void Text_sorts_ignoring_case()
	{
		Row[] rows =
		[
			new("A", "banana"),
			new("B", "Apple"),
			new("C", "cherry"),
			new("D", "apricot"),
		];

		Assert.Equal(["B", "D", "A", "C"], Order(rows));
	}

	[Fact]
	public void Numbers_come_before_text_ascending_and_after_it_descending()
	{
		Row[] rows = [new("A", "buy"), new("B", "12"), new("C", "Hold"), new("D", "3")];

		Assert.Equal(["D", "B", "A", "C"], Order(rows));
		Assert.Equal(["C", "A", "B", "D"], Order(rows, ListSortDirection.Descending));
	}

	[Fact]
	public void Blank_cells_go_last_in_either_direction_in_saved_order()
	{
		Row[] rows = [new("A", ""), new("B", "5"), new("C", null), new("D", "  "), new("E", "1")];

		Assert.Equal(["E", "B", "A", "C", "D"], Order(rows));
		Assert.Equal(["B", "E", "A", "C", "D"], Order(rows, ListSortDirection.Descending));
	}

	[Fact]
	public void Equal_values_keep_the_saved_order_and_pins_stay_on_top()
	{
		Row[] rows =
		[
			new("A", "x"),
			new("B", "X"),
			new("P", "a", Pin: 0),
			new("C", "2.0"),
			new("D", "2"),
		];

		Assert.Equal(["P", "C", "D", "A", "B"], Order(rows));
	}

	[Theory]
	[InlineData("42", 42)]
	[InlineData("-3.5", -3.5)]
	[InlineData("+7", 7)]
	[InlineData("1,234.5", 1234.5)]
	[InlineData("12.5%", 12.5)]
	[InlineData(" 1e3 ", 1000)]
	public void Numbers_are_read_in_invariant_culture(string text, double expected)
	{
		Assert.True(WatchlistSortHelpers.TryParseNumber(text, out var value));
		Assert.Equal(expected, value);
	}

	[Theory]
	[InlineData("1,5")]
	[InlineData("abc")]
	[InlineData("12abc")]
	[InlineData("NaN")]
	[InlineData("Infinity")]
	[InlineData("")]
	public void Other_text_is_not_a_number(string text) =>
		Assert.False(WatchlistSortHelpers.TryParseNumber(text, out _));

	[Fact]
	public void Custom_columns_are_told_apart_by_id_when_clicking_headers()
	{
		var next = WatchlistSortHelpers.NextSort(
			WatchlistSortColumn.Custom,
			"a",
			ListSortDirection.Ascending,
			WatchlistSortColumn.Custom,
			"a"
		);
		Assert.Equal((WatchlistSortColumn.Custom, "a", ListSortDirection.Descending), next);

		next = WatchlistSortHelpers.NextSort(
			WatchlistSortColumn.Custom,
			"a",
			ListSortDirection.Descending,
			WatchlistSortColumn.Custom,
			"b"
		);
		Assert.Equal((WatchlistSortColumn.Custom, "b", ListSortDirection.Ascending), next);

		next = WatchlistSortHelpers.NextSort(
			WatchlistSortColumn.Custom,
			"a",
			ListSortDirection.Ascending,
			WatchlistSortColumn.Last,
			null
		);
		Assert.Equal((WatchlistSortColumn.Last, (string?)null, ListSortDirection.Ascending), next);
	}
}
