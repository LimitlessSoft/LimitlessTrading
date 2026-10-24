using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class WorkspaceHelpersTests
{
	[Fact]
	public void Default_placements_open_everything_but_the_option_chain_in_default_docks()
	{
		var placements = WorkspaceHelpers.GetDefaultPlacements();

		Assert.DoesNotContain(placements, x => x.Tool == WorkspaceTool.OptionChain);
		Assert.Equal(Enum.GetValues<WorkspaceTool>().Length - 1, placements.Count);
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
}
