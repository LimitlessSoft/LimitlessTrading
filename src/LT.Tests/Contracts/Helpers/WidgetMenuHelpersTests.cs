using LT.App.Workspace;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

/// <summary>Menu → Widgets lists IBKR widgets and the ones that work without IB in separate sections.</summary>
public class WidgetMenuHelpersTests
{
	private static WidgetCategory CategoryOf(WorkspaceTool tool) =>
		tool == WorkspaceTool.Notes ? WidgetCategory.Tools : WidgetCategory.Ibkr;

	[Fact]
	public void Group_splits_by_category_keeping_order_and_inner_separators()
	{
		WorkspaceTool?[] order =
		[
			WorkspaceTool.Watchlist,
			WorkspaceTool.Positions,
			null,
			WorkspaceTool.Chart,
			null,
			WorkspaceTool.Notes,
		];

		var sections = WidgetMenuHelpers.Group(order, CategoryOf);

		Assert.Equal([WidgetCategory.Ibkr, WidgetCategory.Tools], sections.Select(x => x.Category));
		Assert.Equal(
			[WorkspaceTool.Watchlist, WorkspaceTool.Positions, null, WorkspaceTool.Chart],
			sections[0].Items
		);
		Assert.Equal([WorkspaceTool.Notes], sections[1].Items);
	}

	[Fact]
	public void Group_drops_separators_at_the_ends_and_doubled_ones()
	{
		WorkspaceTool?[] order =
		[
			null,
			WorkspaceTool.Watchlist,
			null,
			null,
			WorkspaceTool.Chart,
			null,
		];

		var section = Assert.Single(WidgetMenuHelpers.Group(order, CategoryOf));

		Assert.Equal([WorkspaceTool.Watchlist, null, WorkspaceTool.Chart], section.Items);
	}

	[Fact]
	public void Menu_lists_every_widget_once_with_ibkr_first()
	{
		var sections = WidgetsMenuItem.Sections;

		Assert.Equal([WidgetCategory.Ibkr, WidgetCategory.Tools], sections.Select(x => x.Category));
		var tools = sections.SelectMany(x => x.Items).OfType<WorkspaceTool>().ToList();
		Assert.Equal(Enum.GetValues<WorkspaceTool>().Order(), tools.Order());
		Assert.All(
			sections,
			section =>
				Assert.All(
					section.Items.OfType<WorkspaceTool>(),
					tool => Assert.Equal(section.Category, WidgetCatalog.GetCategory(tool))
				)
		);
		Assert.Equal([WorkspaceTool.Notes], sections[1].Items);
	}

	[Fact]
	public void Notes_works_without_ib() =>
		Assert.Equal(WidgetCategory.Tools, WidgetCatalog.GetCategory(WorkspaceTool.Notes));
}
