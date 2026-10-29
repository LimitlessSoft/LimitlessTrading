using LT.App.Stores;
using LT.App.Widgets.News;
using LT.App.Workspace;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.News;
using LT.Contracts.Enums;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// A headline's provider badge is colored by where it comes from, with the frame corners' colors:
/// IB's providers in the IBKR color, the app's own feeds in the Tools color.
/// </summary>
public class NewsRowSourceTests
{
	private readonly TestCache _cache = new();

	private NewsRowViewModel Row(string sourceId, string provider) =>
		new(
			new NewsHeadlineDto
			{
				SourceId = sourceId,
				ProviderCode = provider,
				ArticleId = "1",
				Headline = "Headline",
			},
			new DisplayTimeStore(
				new SettingsManager(_cache.Settings, new FakeIbkrApiClient()),
				_cache.Time
			),
			isMatch: false
		);

	[Fact]
	public void An_ib_headline_has_the_ibkr_badge()
	{
		var row = Row(NewsConstants.IbkrSourceId, "BRFG");

		Assert.Equal(WidgetCategory.Ibkr, row.SourceCategory);
		Assert.Same(WidgetCategoryBrushes.Ibkr, row.SourceBrush);
		Assert.Equal("BRFG · IB", row.SourceTip);
	}

	[Fact]
	public void An_own_feed_headline_has_the_tools_badge()
	{
		var row = Row(FeedNewsConstants.SourceId, "SEC");

		Assert.Equal(WidgetCategory.Tools, row.SourceCategory);
		Assert.Same(WidgetCategoryBrushes.Tools, row.SourceBrush);
		Assert.Equal("SEC · LimitlessTrading feed", row.SourceTip);
	}
}
