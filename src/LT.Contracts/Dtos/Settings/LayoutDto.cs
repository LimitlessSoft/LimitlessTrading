using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Settings;

/// <summary>The main window's workspace and size, and the extra workspace windows.</summary>
public class LayoutDto : WorkspaceLayoutDto
{
	public ChartTimeframe ChartTimeframe { get; set; }
	public double WindowWidth { get; set; }
	public double WindowHeight { get; set; }
	public bool IsMaximized { get; set; }

	/// <summary>Normal-state position in device pixels; null = centered.</summary>
	public double? WindowX { get; set; }
	public double? WindowY { get; set; }

	/// <summary>Null = older layouts: <see cref="IsMaximized"/> decides.</summary>
	public WindowPlacementState? WindowState { get; set; }
	public WindowScreenDto? WindowScreen { get; set; }
	public List<WorkspaceWindowDto> Windows { get; set; } = [];
}
