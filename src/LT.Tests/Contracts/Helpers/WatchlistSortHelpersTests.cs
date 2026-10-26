using System.ComponentModel;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class WatchlistSortHelpersTests
{
	private sealed record Row(string Symbol, double? Last, int? Pin = null);

	// Saved order; MSFT pinned first, AAPL second
	private static readonly Row[] Rows =
	[
		new("SPY", 500),
		new("AAPL", 200, Pin: 1),
		new("NVDA", 900),
		new("XYZ", null),
		new("MSFT", 400, Pin: 0),
		new("TSLA", 250),
	];

	private static string[] Order(
		WatchlistSortColumn column,
		ListSortDirection direction = ListSortDirection.Ascending
	) =>
		WatchlistSortHelpers
			.Order(
				Rows,
				x => x.Pin,
				column,
				direction,
				(x, c) =>
					c switch
					{
						WatchlistSortColumn.Symbol => x.Symbol,
						WatchlistSortColumn.Last => x.Last,
						_ => null,
					}
			)
			.Select(x => x.Symbol)
			.ToArray();

	[Fact]
	public void Unsorted_keeps_pins_on_top_in_pin_order_and_the_rest_in_saved_order() =>
		Assert.Equal(
			["MSFT", "AAPL", "SPY", "NVDA", "XYZ", "TSLA"],
			Order(WatchlistSortColumn.None)
		);

	[Theory]
	[InlineData(
		ListSortDirection.Ascending,
		new[] { "MSFT", "AAPL", "TSLA", "SPY", "NVDA", "XYZ" }
	)]
	[InlineData(
		ListSortDirection.Descending,
		new[] { "MSFT", "AAPL", "NVDA", "SPY", "TSLA", "XYZ" }
	)]
	public void A_numeric_sort_orders_only_the_unpinned_rows_and_puts_unknown_values_last(
		ListSortDirection direction,
		string[] expected
	) => Assert.Equal(expected, Order(WatchlistSortColumn.Last, direction));

	[Theory]
	[InlineData(
		ListSortDirection.Ascending,
		new[] { "MSFT", "AAPL", "NVDA", "SPY", "TSLA", "XYZ" }
	)]
	[InlineData(
		ListSortDirection.Descending,
		new[] { "MSFT", "AAPL", "XYZ", "TSLA", "SPY", "NVDA" }
	)]
	public void Pinned_rows_ignore_the_symbol_sort_too(
		ListSortDirection direction,
		string[] expected
	) => Assert.Equal(expected, Order(WatchlistSortColumn.Symbol, direction));

	[Fact]
	public void Ties_keep_the_saved_order_in_both_directions()
	{
		Row[] rows = [new("B", 1), new("A", 1), new("C", 2)];

		string[] Sort(ListSortDirection direction) =>
			WatchlistSortHelpers
				.Order(rows, x => x.Pin, WatchlistSortColumn.Last, direction, (x, _) => x.Last)
				.Select(x => x.Symbol)
				.ToArray();

		Assert.Equal(["B", "A", "C"], Sort(ListSortDirection.Ascending));
		Assert.Equal(["C", "B", "A"], Sort(ListSortDirection.Descending));
	}

	[Fact]
	public void Symbols_sort_ignoring_case()
	{
		Row[] rows = [new("b", null), new("A", null), new("C", null)];

		var sorted = WatchlistSortHelpers.Order(
			rows,
			x => x.Pin,
			WatchlistSortColumn.Symbol,
			ListSortDirection.Ascending,
			(x, _) => x.Symbol
		);

		Assert.Equal(["A", "b", "C"], sorted.Select(x => x.Symbol));
	}

	[Fact]
	public void A_new_column_sorts_ascending_and_the_same_one_flips()
	{
		Assert.Equal(
			(WatchlistSortColumn.Volume, ListSortDirection.Ascending),
			WatchlistSortHelpers.NextSort(
				WatchlistSortColumn.Last,
				ListSortDirection.Descending,
				WatchlistSortColumn.Volume
			)
		);
		Assert.Equal(
			(WatchlistSortColumn.Last, ListSortDirection.Descending),
			WatchlistSortHelpers.NextSort(
				WatchlistSortColumn.Last,
				ListSortDirection.Ascending,
				WatchlistSortColumn.Last
			)
		);
		Assert.Equal(
			(WatchlistSortColumn.Last, ListSortDirection.Ascending),
			WatchlistSortHelpers.NextSort(
				WatchlistSortColumn.Last,
				ListSortDirection.Descending,
				WatchlistSortColumn.Last
			)
		);
	}

	[Theory]
	[InlineData(WatchlistSortColumn.None, false)]
	[InlineData(WatchlistSortColumn.Symbol, false)]
	[InlineData(WatchlistSortColumn.Last, true)]
	[InlineData(WatchlistSortColumn.ChangePercent, true)]
	[InlineData(WatchlistSortColumn.Bid, true)]
	[InlineData(WatchlistSortColumn.Volume, true)]
	[InlineData(WatchlistSortColumn.MarketCap, true)]
	public void Only_quote_columns_resort_as_quotes_tick(
		WatchlistSortColumn column,
		bool expected
	) => Assert.Equal(expected, WatchlistSortHelpers.DependsOnQuotes(column));
}
