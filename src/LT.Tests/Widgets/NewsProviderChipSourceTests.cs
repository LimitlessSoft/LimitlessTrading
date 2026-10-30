using LT.App.Widgets.News;
using LT.App.Workspace;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.News;
using LT.Contracts.Enums;

namespace LT.Tests.Widgets;

/// <summary>
/// A provider chip in the filter row is colored by where the provider comes from, like the
/// headline badges: IB's providers in the IBKR color, the app's own feeds in the Tools color.
/// </summary>
public class NewsProviderChipSourceTests
{
	private static NewsProviderChip Chip(string sourceId, string code, string name) =>
		new(
			new NewsProviderDto
			{
				SourceId = sourceId,
				Code = code,
				Name = name,
			}
		);

	[Fact]
	public void An_ib_provider_has_the_ibkr_color()
	{
		var chip = Chip(NewsConstants.IbkrSourceId, "DJ-N", "Dow Jones Newsletters");

		Assert.Equal(WidgetCategory.Ibkr, chip.SourceCategory);
		Assert.Same(WidgetCategoryBrushes.Ibkr, chip.SourceBrush);
		Assert.Equal("Dow Jones Newsletters (DJ-N) · IB", chip.Tip);
	}

	[Fact]
	public void An_own_feed_provider_has_the_tools_color()
	{
		var chip = Chip(FeedNewsConstants.SourceId, "PRN", "PR Newswire");

		Assert.Equal(WidgetCategory.Tools, chip.SourceCategory);
		Assert.Same(WidgetCategoryBrushes.Tools, chip.SourceBrush);
		Assert.Equal("PR Newswire (PRN) · LimitlessTrading feed", chip.Tip);
	}
}
