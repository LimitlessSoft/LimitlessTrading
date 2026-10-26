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

	/// <summary>Center split tree; null = built from the center placements (side by side).</summary>
	public CenterNodeEntity? Center { get; set; }

	/// <summary>Timeframe of the last chart used; new charts start with it.</summary>
	public ChartTimeframe ChartTimeframe { get; set; } = ChartTimeframe.FiveMinutes;
	public double WindowWidth { get; set; } = 1440;
	public double WindowHeight { get; set; } = 900;
	public bool IsMaximized { get; set; }
}
