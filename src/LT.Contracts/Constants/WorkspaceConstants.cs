namespace LT.Contracts.Constants;

public static class WorkspaceConstants
{
	// First-run workspace: side and bottom panel sizes, and each side widget's own width
	public const double DefaultLeftWidth = 441;
	public const double DefaultRightWidth = 635;
	public const double DefaultBottomHeight = 240;
	public const double DefaultWatchlistWidth = 441;
	public const double DefaultRightWidgetWidth = 407;
	public const double DefaultOptionChainWidth = 1100;

	public const double FloatingDefaultWidth = 520;
	public const double FloatingDefaultHeight = 600;
	public const double FloatingMinWidth = 260;
	public const double FloatingMinHeight = 200;
	public const double FloatingCascadeOffset = 40;

	/// <summary>Movement (DIPs) before a press on a drag handle (frame header, strip icon) turns into a drag.</summary>
	public const double DragThreshold = 6;

	/// <summary>Name of the first center view, and of the single center of older layouts.</summary>
	public const string DefaultViewName = "Main";

	/// <summary>New center views are "View 2", "View 3", …</summary>
	public const string NewViewNamePrefix = "View";

	public const int ViewNameMaxLength = 40;

	/// <summary>A duplicated view is "&lt;name&gt; copy", then "&lt;name&gt; copy 2", …</summary>
	public const string DuplicateViewNameSuffix = "copy";

	/// <summary>Hovering a dragged widget this long over a view tab switches to that view.</summary>
	public const int ViewTabHoverSwitchMs = 600;

	// Extra workspace windows (menu → New window): start size and the smallest they can get
	public const double WindowDefaultWidth = 1280;
	public const double WindowDefaultHeight = 800;
	public const double WindowMinWidth = 720;
	public const double WindowMinHeight = 420;

	/// <summary>Extra windows are "Window 2", "Window 3", … (the main window counts as the first).</summary>
	public const string NewWindowNamePrefix = "Window";
}
