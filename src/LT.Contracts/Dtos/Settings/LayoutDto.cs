using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Settings;

/// <summary>The main window's workspace and size, and the extra workspace windows.</summary>
public class LayoutDto : WorkspaceLayoutDto
{
	public ChartTimeframe ChartTimeframe { get; set; }
	public double WindowWidth { get; set; }
	public double WindowHeight { get; set; }
	public bool IsMaximized { get; set; }
	public List<WorkspaceWindowDto> Windows { get; set; } = [];
}
