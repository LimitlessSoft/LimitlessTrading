using LT.App.Workspace;
using LT.Contracts.Enums;

namespace LT.Tests.Workspace;

/// <summary>
/// The Widgets menu shows no shortcuts, so a dock icon's tooltip is where a widget's shortcut is
/// shown — including Ctrl+1…9 for widgets without a Ctrl+Alt focus key.
/// </summary>
public class WidgetShortcutTests
{
	[Theory]
	[InlineData(WorkspaceTool.Watchlist, "Ctrl+Alt+W")]
	[InlineData(WorkspaceTool.News, "Ctrl+Alt+N")]
	[InlineData(WorkspaceTool.Chart, "Ctrl+7")]
	public void GetShortcut_ReturnsTheKeysThatOpenTheWidget(WorkspaceTool tool, string expected) =>
		Assert.Equal(expected, WidgetCatalog.GetShortcut(tool));

	[Fact]
	public void DockToolTip_ShowsTheChartHotkey() =>
		Assert.Equal(
			"Chart (Ctrl+7)",
			new WidgetItem("c1", WorkspaceTool.Chart, new object()).ToolTip
		);

	[Fact]
	public void DockToolTip_ShowsTheFocusShortcut() =>
		Assert.Equal(
			"Watchlist (Ctrl+Alt+W)",
			new WidgetItem("w1", WorkspaceTool.Watchlist, new object()).ToolTip
		);

	[Fact]
	public void Frame_corner_marker_names_and_colors_the_source()
	{
		var ibkr = new WidgetItem("w1", WorkspaceTool.Watchlist, new object());
		var notes = new WidgetItem("n1", WorkspaceTool.Notes, new object());

		Assert.Equal("Interactive Brokers", ibkr.CategoryToolTip);
		Assert.Equal("Tools", notes.CategoryToolTip);
		Assert.NotSame(ibkr.CategoryBrush, notes.CategoryBrush);
	}

	[Fact]
	public void News_corner_marker_is_mixed_ib_and_own_sources()
	{
		var news = new WidgetItem("n1", WorkspaceTool.News, new object());

		Assert.Equal(WidgetCategory.Mixed, news.Category);
		Assert.Equal("Interactive Brokers + own sources", news.CategoryToolTip);
		Assert.Same(WidgetCategoryBrushes.Mixed, news.CategoryBrush);
		// Both colors: IB's and the tools'
		var stops = Assert
			.IsAssignableFrom<Avalonia.Media.IGradientBrush>(news.CategoryBrush)
			.GradientStops.Select(x => x.Color.ToString())
			.Distinct();
		Assert.Equal(["#ffb8973a", "#ff5c6bc0"], stops);
	}
}
