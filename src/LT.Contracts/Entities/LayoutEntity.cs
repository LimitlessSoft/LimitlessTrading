using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Contracts.Entities;

/// <summary>The main window's workspace, its size, and the extra workspace windows.</summary>
public class LayoutEntity : WorkspaceLayoutEntity
{
	public LayoutEntity() => Widgets = WorkspaceHelpers.GetDefaultPlacements();

	/// <summary>Timeframe of the last chart used; new charts start with it.</summary>
	public ChartTimeframe ChartTimeframe { get; set; } = ChartTimeframe.FiveMinutes;
	public double WindowWidth { get; set; } = 1440;
	public double WindowHeight { get; set; } = 900;
	public bool IsMaximized { get; set; } = true;

	/// <summary>Normal-state position in device pixels; null = centered (older layouts).</summary>
	public double? WindowX { get; set; }
	public double? WindowY { get; set; }

	/// <summary>Null = older layouts: <see cref="IsMaximized"/> decides.</summary>
	public WindowPlacementState? WindowState { get; set; }

	/// <summary>The screen the main window was on; null in older layouts.</summary>
	public WindowScreenEntity? WindowScreen { get; set; }

	/// <summary>Extra workspace windows open at exit, reopened on start; none in older layouts.</summary>
	public List<WorkspaceWindowEntity> Windows { get; set; } = [];
}
