using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class CenterLayoutHelpersTests
{
	// Compact tree description: leaf = id, split = H[...] / V[...]
	private static string Describe(CenterNodeDto? node) =>
		node is null ? "empty"
		: node.IsLeaf ? node.WidgetId!
		: $"{(node.Orientation == SplitOrientation.Horizontal ? "H" : "V")}[{string.Join(",", node.Children.Select(Describe))}]";

	[Fact]
	public void Insert_into_empty_center_makes_a_single_leaf() =>
		Assert.Equal("A", Describe(CenterLayoutHelpers.Insert(null, "A")));

	[Theory]
	[InlineData(DropEdge.Left, "H[B,A]")]
	[InlineData(DropEdge.Right, "H[A,B]")]
	[InlineData(DropEdge.Top, "V[B,A]")]
	[InlineData(DropEdge.Bottom, "V[A,B]")]
	public void Insert_on_an_edge_splits_the_target(DropEdge edge, string expected)
	{
		var root = CenterLayoutHelpers.Insert(null, "A");

		Assert.Equal(expected, Describe(CenterLayoutHelpers.Insert(root, "B", "A", edge)));
	}

	[Fact]
	public void Insert_below_one_of_two_side_by_side_widgets_nests_a_vertical_split()
	{
		var root = CenterLayoutHelpers.Insert(null, "Chart");
		root = CenterLayoutHelpers.Insert(root, "Options", "Chart", DropEdge.Right);

		root = CenterLayoutHelpers.Insert(root, "Chart-2", "Chart", DropEdge.Bottom);

		Assert.Equal("H[V[Chart,Chart-2],Options]", Describe(root));
	}

	[Fact]
	public void Insert_in_the_same_direction_joins_the_existing_row_and_halves_the_target()
	{
		var root = CenterLayoutHelpers.Insert(null, "A");
		root = CenterLayoutHelpers.Insert(root, "B", "A", DropEdge.Right);

		root = CenterLayoutHelpers.Insert(root, "C", "A", DropEdge.Right);

		Assert.Equal("H[A,C,B]", Describe(root));
		Assert.Equal([0.5, 0.5, 1], root.Children.Select(x => x.Size));
	}

	[Fact]
	public void Insert_without_a_target_adds_to_the_right_of_everything()
	{
		var root = CenterLayoutHelpers.Insert(null, "A");
		root = CenterLayoutHelpers.Insert(root, "B", "A", DropEdge.Bottom);

		Assert.Equal("H[V[A,B],C]", Describe(CenterLayoutHelpers.Insert(root, "C")));
	}

	[Fact]
	public void Remove_collapses_a_split_left_with_one_child()
	{
		var root = CenterLayoutHelpers.Insert(null, "A");
		root = CenterLayoutHelpers.Insert(root, "B", "A", DropEdge.Right);
		root = CenterLayoutHelpers.Insert(root, "C", "B", DropEdge.Bottom);

		root = CenterLayoutHelpers.Remove(root, "C");
		Assert.Equal("H[A,B]", Describe(root));

		root = CenterLayoutHelpers.Remove(root, "A");
		Assert.Equal("B", Describe(root));

		Assert.Null(CenterLayoutHelpers.Remove(root, "B"));
	}

	[Fact]
	public void Remove_merges_a_nested_split_that_ends_up_in_the_same_direction()
	{
		var root = CenterLayoutHelpers.Insert(null, "A");
		root = CenterLayoutHelpers.Insert(root, "B", "A", DropEdge.Bottom);
		root = CenterLayoutHelpers.Insert(root, "C", "B", DropEdge.Right);
		root = CenterLayoutHelpers.Insert(root, "D", "A", DropEdge.Right);
		Assert.Equal("V[H[A,D],H[B,C]]", Describe(root));

		// Without B and D the tree is a column; no single-child splits are left behind
		root = CenterLayoutHelpers.Remove(root, "D");
		root = CenterLayoutHelpers.Remove(root, "B");

		Assert.Equal("V[A,C]", Describe(root));
	}

	[Fact]
	public void Normalize_drops_unknown_and_duplicate_leaves_and_adds_missing_widgets()
	{
		var saved = new CenterNodeDto
		{
			Orientation = SplitOrientation.Vertical,
			Children =
			[
				CenterLayoutHelpers.Leaf("A"),
				CenterLayoutHelpers.Leaf("Gone"),
				CenterLayoutHelpers.Leaf("A"),
			],
		};

		var root = CenterLayoutHelpers.Normalize(saved, ["A", "B"]);

		Assert.Equal("H[A,B]", Describe(root));
	}

	[Fact]
	public void GetWidgetIds_lists_leaves_in_layout_order()
	{
		var root = CenterLayoutHelpers.Insert(null, "A");
		root = CenterLayoutHelpers.Insert(root, "B", "A", DropEdge.Top);
		root = CenterLayoutHelpers.Insert(root, "C", "A", DropEdge.Right);

		Assert.Equal(["B", "A", "C"], CenterLayoutHelpers.GetWidgetIds(root));
	}
}
