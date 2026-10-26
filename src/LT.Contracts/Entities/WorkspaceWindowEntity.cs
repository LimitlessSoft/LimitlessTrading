using LT.Contracts.Constants;
using LT.Contracts.Enums;

namespace LT.Contracts.Entities;

/// <summary>An extra workspace window: its own workspace, name and bounds.</summary>
public class WorkspaceWindowEntity : WorkspaceLayoutEntity
{
	public string Id { get; set; } = Guid.NewGuid().ToString("N");

	/// <summary>Shown in its title bar: "Window 2", "Window 3", …</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Position in device pixels; null = centered on the screen.</summary>
	public double? X { get; set; }
	public double? Y { get; set; }
	public double Width { get; set; } = WorkspaceConstants.WindowDefaultWidth;
	public double Height { get; set; } = WorkspaceConstants.WindowDefaultHeight;
	public bool IsMaximized { get; set; }

	/// <summary>Null = older layouts: <see cref="IsMaximized"/> decides.</summary>
	public WindowPlacementState? State { get; set; }

	/// <summary>The screen the window was on; null in older layouts.</summary>
	public WindowScreenEntity? Screen { get; set; }
}
