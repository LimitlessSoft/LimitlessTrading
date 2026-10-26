using LT.Contracts.Constants;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class CenterViewHelpersTests
{
	// Compact tree description: leaf = id, split = H[...] / V[...]
	private static string Describe(CenterNodeDto? node) =>
		node is null ? "empty"
		: node.IsLeaf ? node.WidgetId!
		: $"{(node.Orientation == SplitOrientation.Horizontal ? "H" : "V")}[{string.Join(",", node.Children.Select(Describe))}]";

	private static CenterNodeDto Tree(params string[] ids) =>
		ids.Aggregate((CenterNodeDto?)null, (root, id) => CenterLayoutHelpers.Insert(root, id))!;

	[Fact]
	public void A_layout_saved_before_views_becomes_one_view_called_Main_with_its_tree()
	{
		var legacy = CenterLayoutHelpers.Insert(Tree("Chart"), "Options", "Chart", DropEdge.Bottom);

		var views = CenterViewHelpers.Normalize([], legacy, null, ["Chart", "Options"]);

		var view = Assert.Single(views);
		Assert.Equal(WorkspaceConstants.DefaultViewName, view.Name);
		Assert.False(string.IsNullOrEmpty(view.Id));
		Assert.Equal("V[Chart,Options]", Describe(view.Root));
	}

	[Fact]
	public void A_layout_saved_before_views_without_a_tree_puts_its_center_widgets_side_by_side()
	{
		var views = CenterViewHelpers.Normalize(null, null, null, ["Chart", "Options"]);

		Assert.Equal("H[Chart,Options]", Describe(Assert.Single(views).Root));
	}

	[Fact]
	public void Saved_views_are_kept_and_the_old_center_tree_is_ignored()
	{
		List<CenterViewDto> saved =
		[
			new()
			{
				Id = "a",
				Name = "Main",
				Root = Tree("Chart"),
			},
			new()
			{
				Id = "b",
				Name = "Options",
				Root = Tree("Options"),
			},
		];

		var views = CenterViewHelpers.Normalize(saved, Tree("Stale"), "b", ["Chart", "Options"]);

		Assert.Equal(
			[("a", "Main", "Chart"), ("b", "Options", "Options")],
			views.Select(x => (x.Id, x.Name, Describe(x.Root)))
		);
	}

	[Fact]
	public void An_empty_view_stays_a_view()
	{
		List<CenterViewDto> saved =
		[
			new()
			{
				Id = "a",
				Name = "Main",
				Root = Tree("Chart"),
			},
			new() { Id = "b", Name = "View 2" },
		];

		var views = CenterViewHelpers.Normalize(saved, null, "a", ["Chart"]);

		Assert.Equal(["Chart", "empty"], views.Select(x => Describe(x.Root)));
	}

	[Fact]
	public void A_widget_in_two_views_is_kept_in_the_first_only()
	{
		List<CenterViewDto> saved =
		[
			new()
			{
				Id = "a",
				Name = "Main",
				Root = Tree("Chart", "Options"),
			},
			new()
			{
				Id = "b",
				Name = "View 2",
				Root = Tree("Chart", "Chart-2"),
			},
		];

		var views = CenterViewHelpers.Normalize(saved, null, "a", ["Chart", "Options", "Chart-2"]);

		Assert.Equal(["H[Chart,Options]", "Chart-2"], views.Select(x => Describe(x.Root)));
	}

	[Fact]
	public void Closed_widgets_drop_out_and_center_widgets_in_no_view_join_the_selected_one()
	{
		List<CenterViewDto> saved =
		[
			new()
			{
				Id = "a",
				Name = "Main",
				Root = Tree("Chart", "Closed"),
			},
			new()
			{
				Id = "b",
				Name = "View 2",
				Root = Tree("Options"),
			},
		];

		var views = CenterViewHelpers.Normalize(saved, null, "b", ["Chart", "Options", "News"]);

		Assert.Equal(["Chart", "H[Options,News]"], views.Select(x => Describe(x.Root)));
	}

	[Fact]
	public void Duplicate_or_missing_ids_and_blank_names_are_repaired()
	{
		List<CenterViewDto> saved =
		[
			new() { Id = "a", Name = "Main" },
			new() { Id = "a", Name = "  " },
			new() { Id = "", Name = "Mine" },
		];

		var views = CenterViewHelpers.Normalize(saved, null, null, []);

		Assert.Equal(3, views.Select(x => x.Id).Distinct().Count());
		Assert.DoesNotContain(views, x => string.IsNullOrEmpty(x.Id));
		Assert.Equal(["Main", "View 2", "Mine"], views.Select(x => x.Name));
	}

	[Theory]
	[InlineData("b", "b")]
	[InlineData("missing", "a")]
	[InlineData(null, "a")]
	public void Selected_view_falls_back_to_the_first(string? selected, string expected)
	{
		List<CenterViewDto> views = [new() { Id = "a" }, new() { Id = "b" }];

		Assert.Equal(expected, CenterViewHelpers.GetSelectedId(views, selected));
	}

	[Fact]
	public void New_views_take_the_first_free_number_from_two() =>
		Assert.Equal("View 3", CenterViewHelpers.GetNewName(["Main", "view 2", "View 4"]));

	[Theory]
	[InlineData("  Scalping  ", "Scalping")]
	[InlineData("   ", null)]
	[InlineData("", null)]
	[InlineData(null, null)]
	public void Typed_names_are_trimmed_and_blank_ones_rejected(string? typed, string? expected) =>
		Assert.Equal(expected, CenterViewHelpers.CleanName(typed));

	[Fact]
	public void Long_names_are_cut_to_the_limit() =>
		Assert.Equal(
			WorkspaceConstants.ViewNameMaxLength,
			CenterViewHelpers.CleanName(new string('x', 100))!.Length
		);

	[Theory]
	[InlineData(0, 3, 1, 1)]
	[InlineData(2, 3, 1, 0)]
	[InlineData(0, 3, -1, 2)]
	[InlineData(1, 3, -1, 0)]
	[InlineData(0, 1, 1, 0)]
	public void Cycling_wraps_around_both_ends(int index, int count, int step, int expected) =>
		Assert.Equal(expected, CenterViewHelpers.Cycle(index, count, step));

	[Theory]
	[InlineData(0, 2, 0)] // first closed: the next one (now first)
	[InlineData(1, 2, 1)] // middle closed: the next one
	[InlineData(2, 2, 1)] // last closed: the previous one
	public void Closing_a_tab_selects_the_next_else_the_previous(
		int closedIndex,
		int remaining,
		int expected
	) => Assert.Equal(expected, CenterViewHelpers.GetIndexAfterClose(closedIndex, remaining));
}
