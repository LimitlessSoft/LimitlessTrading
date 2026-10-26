using LT.Contracts.Constants;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class WorkspaceHelpersTests
{
	[Fact]
	public void Default_placements_open_everything_but_the_option_chain_and_order_ticket()
	{
		var placements = WorkspaceHelpers.GetDefaultPlacements();

		Assert.DoesNotContain(placements, x => x.Tool == WorkspaceTool.OptionChain);
		Assert.DoesNotContain(placements, x => x.Tool == WorkspaceTool.OrderTicket);
		Assert.Equal(Enum.GetValues<WorkspaceTool>().Length - 2, placements.Count);
		Assert.All(placements, x => Assert.Equal(WorkspaceHelpers.GetDefaultDock(x.Tool), x.Dock));
		Assert.All(placements, x => Assert.Equal(WorkspaceHelpers.GetDefaultId(x.Tool), x.Id));
	}

	[Fact]
	public void Only_charts_allow_several_instances() =>
		Assert.Equal(
			[WorkspaceTool.Chart],
			Enum.GetValues<WorkspaceTool>().Where(WorkspaceHelpers.AllowsMultipleInstances)
		);

	[Fact]
	public void Normalize_keeps_several_charts_but_one_of_everything_else()
	{
		var result = WorkspaceHelpers.Normalize([
			new WidgetPlacementDto { Id = "Chart", Tool = WorkspaceTool.Chart },
			new WidgetPlacementDto { Id = "Chart-2", Tool = WorkspaceTool.Chart },
			new WidgetPlacementDto { Id = "Chart-2", Tool = WorkspaceTool.Chart },
			new WidgetPlacementDto { Id = "Orders", Tool = WorkspaceTool.Orders },
			new WidgetPlacementDto { Id = "Orders-2", Tool = WorkspaceTool.Orders },
			new WidgetPlacementDto { Tool = (WorkspaceTool)99 },
			new WidgetPlacementDto { Tool = WorkspaceTool.Account, Dock = (WidgetDock)99 },
		]);

		Assert.Equal(["Chart", "Chart-2", "Orders"], result.Select(x => x.Id));
	}

	[Fact]
	public void Normalize_gives_placements_from_older_layouts_their_default_id()
	{
		var result = WorkspaceHelpers.Normalize([
			new WidgetPlacementDto { Tool = WorkspaceTool.Watchlist, Dock = WidgetDock.Left },
		]);

		Assert.Equal("Watchlist", result.Single().Id);
	}

	[Fact]
	public void Normalize_keeps_bottom_placements() =>
		Assert.Equal(
			WidgetDock.Bottom,
			WorkspaceHelpers
				.Normalize([
					new WidgetPlacementDto
					{
						Tool = WorkspaceTool.Messages,
						Dock = WidgetDock.Bottom,
					},
				])
				.Single()
				.Dock
		);

	[Theory]
	[InlineData(0, 0)]
	[InlineData(5.9, 0)]
	[InlineData(0, -5.9)]
	[InlineData(-5.9, 5.9)]
	public void A_press_moved_less_than_the_threshold_stays_a_click(double dx, double dy) =>
		Assert.False(WorkspaceHelpers.IsBeyondDragThreshold(dx, dy));

	[Theory]
	[InlineData(WorkspaceConstants.DragThreshold, 0)]
	[InlineData(0, WorkspaceConstants.DragThreshold)]
	[InlineData(-WorkspaceConstants.DragThreshold, 0)]
	[InlineData(0, -12)]
	[InlineData(3, 7)]
	public void A_press_moved_to_the_threshold_on_either_axis_is_a_drag(double dx, double dy) =>
		Assert.True(WorkspaceHelpers.IsBeyondDragThreshold(dx, dy));

	[Fact]
	public void Focus_returns_to_the_center_widget_focused_last() =>
		Assert.Equal(
			"Chart-2",
			WorkspaceHelpers.GetFocusReturnId("Chart-2", ["Chart", "Chart-2", "OptionChain"])
		);

	[Fact]
	public void Focus_returns_to_the_first_center_widget_when_none_was_focused() =>
		Assert.Equal("Chart", WorkspaceHelpers.GetFocusReturnId(null, ["Chart", "OptionChain"]));

	[Fact]
	public void Focus_returns_to_the_first_center_widget_when_the_last_one_left_the_center() =>
		Assert.Equal(
			"Chart",
			WorkspaceHelpers.GetFocusReturnId("Chart-2", ["Chart", "OptionChain"])
		);

	[Fact]
	public void Focus_has_nowhere_to_return_with_an_empty_center() =>
		Assert.Null(WorkspaceHelpers.GetFocusReturnId("Chart", []));
}
