using LT.Contracts.Constants;

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
}
