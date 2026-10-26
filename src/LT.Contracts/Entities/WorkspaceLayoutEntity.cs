using LT.Contracts.Constants;

namespace LT.Contracts.Entities;

/// <summary>
/// One workspace: its side panels, widgets and center views. The main window's is
/// <see cref="LayoutEntity"/>; each extra window has its own (<see cref="WorkspaceWindowEntity"/>).
/// </summary>
public class WorkspaceLayoutEntity
{
	public double LeftWidth { get; set; } = WorkspaceConstants.DefaultLeftWidth;
	public double RightWidth { get; set; } = WorkspaceConstants.DefaultRightWidth;
	public double BottomHeight { get; set; } = WorkspaceConstants.DefaultBottomHeight;

	/// <summary>
	/// Expanded widget instance of each side; null = that side is collapsed (all of them on first
	/// run, leaving the window to the chart).
	/// </summary>
	public string? LeftWidgetId { get; set; }
	public string? RightWidgetId { get; set; }
	public string? BottomWidgetId { get; set; }

	/// <summary>The open widgets; an extra window starts with none.</summary>
	public List<WidgetPlacementEntity> Widgets { get; set; } = [];

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
}
