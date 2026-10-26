using LT.Contracts.Constants;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class WorkspaceHelpersTests
{
	[Fact]
	public void Default_placements_put_the_chart_in_the_center_and_the_watchlist_left_and_four_widgets_right()
	{
		var placements = WorkspaceHelpers.GetDefaultPlacements();

		Assert.Equal(
			[
				(WorkspaceTool.Chart, WidgetDock.Center, true, (double?)null),
				(WorkspaceTool.Watchlist, WidgetDock.Left, false, 441),
				(WorkspaceTool.Account, WidgetDock.Right, true, 407),
				(WorkspaceTool.Messages, WidgetDock.Right, true, 407),
				(WorkspaceTool.OptionChain, WidgetDock.Right, false, 1100),
				(WorkspaceTool.OrderTicket, WidgetDock.Right, false, 407),
			],
			placements.Select(x => (x.Tool, x.Dock, x.IsPinned, x.SideWidth))
		);
		Assert.All(placements, x => Assert.Equal(WorkspaceHelpers.GetDefaultId(x.Tool), x.Id));
		Assert.All(placements, x => Assert.Equal(LinkGroup.Red, x.LinkGroup));
	}

	[Fact]
	public void Default_chart_shows_SPY_at_five_minutes_and_is_linked()
	{
		var chart = WorkspaceHelpers
			.GetDefaultPlacements()
			.Single(x => x.Tool == WorkspaceTool.Chart);

		Assert.Equal(
			(ChartTimeframe.FiveMinutes, true, 756733, "SPY", "SMART", "ARCA", "USD"),
			(
				chart.ChartTimeframe,
				chart.ChartLinked,
				chart.ChartContract!.ConId,
				chart.ChartContract.Symbol,
				chart.ChartContract.Exchange,
				chart.ChartContract.PrimaryExchange,
				chart.ChartContract.Currency
			)
		);
	}

	[Fact]
	public void Default_placements_are_new_instances_on_every_call() =>
		Assert.NotSame(
			WorkspaceHelpers.GetDefaultPlacements()[0].ChartContract,
			WorkspaceHelpers.GetDefaultPlacements()[0].ChartContract
		);

	[Fact]
	public void Default_layout_is_maximized_with_collapsed_sides_and_the_chart_in_the_main_view()
	{
		var layout = new LayoutEntity();

		Assert.Equal(
			(441d, 635d, 240d, (string?)null, (string?)null, (string?)null, true),
			(
				layout.LeftWidth,
				layout.RightWidth,
				layout.BottomHeight,
				layout.LeftWidgetId,
				layout.RightWidgetId,
				layout.BottomWidgetId,
				layout.IsMaximized
			)
		);
		Assert.Equal(ChartTimeframe.FiveMinutes, layout.ChartTimeframe);
		var view = Assert.Single(
			CenterViewHelpers.Normalize(
				[],
				null,
				null,
				layout.Widgets.Where(x => x.Dock == WidgetDock.Center).Select(x => x.Id).ToList()
			)
		);
		Assert.Equal(WorkspaceHelpers.GetDefaultId(WorkspaceTool.Chart), view.Root!.WidgetId);
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
