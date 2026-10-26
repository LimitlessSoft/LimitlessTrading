using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Contracts.Entities;

public class LayoutEntity
{
	public double LeftWidth { get; set; } = 380;
	public double RightWidth { get; set; } = 300;
	public double BottomHeight { get; set; } = 240;

	/// <summary>Expanded widget instance of each side; null = that side is collapsed.</summary>
	public string? LeftWidgetId { get; set; } = nameof(WorkspaceTool.Watchlist);
	public string? RightWidgetId { get; set; } = nameof(WorkspaceTool.Account);
	public string? BottomWidgetId { get; set; }

	public List<WidgetPlacementEntity> Widgets { get; set; } =
		WorkspaceHelpers.GetDefaultPlacements();

	/// <summary>
	/// Tabs of the center, each with its own split tree; empty = layouts saved before views, whose
	/// <see cref="Center"/> becomes the first view ("Main") on load.
	/// </summary>
	public List<CenterViewEntity> Views { get; set; } = [];

	/// <summary>The view shown; null or unknown = the first one.</summary>
	public string? SelectedViewId { get; set; }

	/// <summary>
	/// The single center split tree of layouts saved before views; read once, then saved as null
	/// (the tree lives in <see cref="Views"/>).
	/// </summary>
	public CenterNodeEntity? Center { get; set; }

	/// <summary>Timeframe of the last chart used; new charts start with it.</summary>
	public ChartTimeframe ChartTimeframe { get; set; } = ChartTimeframe.FiveMinutes;
	public double WindowWidth { get; set; } = 1440;
	public double WindowHeight { get; set; } = 900;
	public bool IsMaximized { get; set; }
}
