namespace LT.Contracts.Dtos.Settings;

/// <summary>One workspace: side panels, widgets and center views (main window or an extra one).</summary>
public class WorkspaceLayoutDto
{
	public double LeftWidth { get; set; }
	public double RightWidth { get; set; }
	public double BottomHeight { get; set; }
	public string? LeftWidgetId { get; set; }
	public string? RightWidgetId { get; set; }
	public string? BottomWidgetId { get; set; }
	public List<WidgetPlacementDto> Widgets { get; set; } = [];
	public List<CenterViewDto> Views { get; set; } = [];
	public string? SelectedViewId { get; set; }

	/// <summary>Single center tree of layouts saved before views; moved into the first view on load.</summary>
	public CenterNodeDto? Center { get; set; }
}
