using System.ComponentModel;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class WatchlistColumnHelpersTests
{
	[Fact]
	public void Only_built_in_columns_are_hidden()
	{
		var hidden = WatchlistColumnHelpers.ResolveHidden(
			[
				WatchlistSortColumn.Last,
				WatchlistSortColumn.None,
				WatchlistSortColumn.Custom,
				(WatchlistSortColumn)99,
			],
			shownCustomColumns: 0
		);

		Assert.Equal([WatchlistSortColumn.Last], hidden);
	}

	[Fact]
	public void Every_built_in_column_hidden_keeps_symbol_unless_a_custom_one_is_shown()
	{
		var all = WatchlistColumnHelpers.BuiltInColumns;

		Assert.DoesNotContain(
			WatchlistSortColumn.Symbol,
			WatchlistColumnHelpers.ResolveHidden(all, shownCustomColumns: 0)
		);
		Assert.Equal(
			all.Count,
			WatchlistColumnHelpers.ResolveHidden(all, shownCustomColumns: 1).Count
		);
		Assert.Empty(WatchlistColumnHelpers.ResolveHidden(null, shownCustomColumns: 0));
	}

	[Fact]
	public void Average_volume_is_a_built_in_column_after_volume()
	{
		var columns = WatchlistColumnHelpers.BuiltInColumns.ToList();

		Assert.Equal(
			columns.IndexOf(WatchlistSortColumn.Volume) + 1,
			columns.IndexOf(WatchlistSortColumn.AverageVolume)
		);
		// Persisted as an integer: added after every older value
		Assert.True(WatchlistSortColumn.AverageVolume > WatchlistSortColumn.Custom);
	}

	[Theory]
	[InlineData(false, WatchlistSortColumn.None, "165")]
	[InlineData(true, WatchlistSortColumn.AverageVolume, "165")]
	[InlineData(true, WatchlistSortColumn.Volume, "")]
	[InlineData(true, WatchlistSortColumn.None, "")]
	public void Quote_lines_ask_for_average_volume_while_its_column_is_shown_or_sorted(
		bool hidden,
		WatchlistSortColumn sortColumn,
		string expected
	) =>
		Assert.Equal(
			expected,
			WatchlistColumnHelpers.GetGenericTicks(
				hidden
					? new HashSet<WatchlistSortColumn>
					{
						WatchlistSortColumn.AverageVolume,
						WatchlistSortColumn.AverageVolumeRate,
						WatchlistSortColumn.VolumeRate,
					}
					: [WatchlistSortColumn.VolumeRate],
				sortColumn
			)
		);

	[Theory]
	// Every column shown: both, average first
	[InlineData(new WatchlistSortColumn[0], WatchlistSortColumn.None, "165,295")]
	// Avg Vol/Min needs the average as well
	[InlineData(new[] { WatchlistSortColumn.AverageVolume }, WatchlistSortColumn.None, "165,295")]
	[InlineData(
		new[] { WatchlistSortColumn.AverageVolume, WatchlistSortColumn.AverageVolumeRate },
		WatchlistSortColumn.None,
		"295"
	)]
	[InlineData(new[] { WatchlistSortColumn.VolumeRate }, WatchlistSortColumn.None, "165")]
	// Sorted by a hidden Vol/Min: still asked for
	[InlineData(
		new[]
		{
			WatchlistSortColumn.AverageVolume,
			WatchlistSortColumn.AverageVolumeRate,
			WatchlistSortColumn.VolumeRate,
		},
		WatchlistSortColumn.VolumeRate,
		"295"
	)]
	[InlineData(
		new[]
		{
			WatchlistSortColumn.AverageVolume,
			WatchlistSortColumn.AverageVolumeRate,
			WatchlistSortColumn.VolumeRate,
		},
		WatchlistSortColumn.AverageVolumeRate,
		"165"
	)]
	public void Quote_lines_ask_for_the_volume_rate_and_average_their_columns_need(
		WatchlistSortColumn[] hidden,
		WatchlistSortColumn sortColumn,
		string expected
	) =>
		Assert.Equal(
			expected,
			WatchlistColumnHelpers.GetGenericTicks(hidden.ToHashSet(), sortColumn)
		);

	[Fact]
	public void Volume_rate_columns_follow_average_volume_and_are_persisted_after_it()
	{
		var columns = WatchlistColumnHelpers.BuiltInColumns.ToList();

		Assert.Equal(
			columns.IndexOf(WatchlistSortColumn.AverageVolume) + 1,
			columns.IndexOf(WatchlistSortColumn.VolumeRate)
		);
		Assert.Equal(
			columns.IndexOf(WatchlistSortColumn.VolumeRate) + 1,
			columns.IndexOf(WatchlistSortColumn.AverageVolumeRate)
		);
		Assert.Equal(9, (int)WatchlistSortColumn.VolumeRate);
		Assert.Equal(10, (int)WatchlistSortColumn.AverageVolumeRate);
	}

	[Theory]
	[InlineData(true, 1, false)]
	[InlineData(true, 2, true)]
	[InlineData(false, 1, true)]
	public void The_last_shown_column_cannot_be_hidden(bool isShown, int shown, bool expected) =>
		Assert.Equal(expected, WatchlistColumnHelpers.CanToggle(isShown, shown));

	[Fact]
	public void A_saved_sort_is_restored_with_its_direction()
	{
		Assert.Equal(
			(WatchlistSortColumn.Volume, (string?)null, ListSortDirection.Descending),
			WatchlistSortHelpers.ResolveSort(WatchlistSortColumn.Volume, "ignored", true)
		);
		Assert.Equal(
			(WatchlistSortColumn.Custom, (string?)"c1", ListSortDirection.Ascending),
			WatchlistSortHelpers.ResolveSort(WatchlistSortColumn.Custom, "c1", null)
		);
	}

	[Theory]
	[InlineData(null, null)]
	[InlineData(WatchlistSortColumn.None, null)]
	[InlineData(WatchlistSortColumn.Custom, null)]
	[InlineData((WatchlistSortColumn)99, null)]
	public void A_missing_or_unknown_saved_sort_is_none(WatchlistSortColumn? column, string? id) =>
		Assert.Equal(
			(WatchlistSortColumn.None, (string?)null, ListSortDirection.Ascending),
			WatchlistSortHelpers.ResolveSort(column, id, true)
		);
}
