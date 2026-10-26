namespace LT.Contracts.Dtos.Settings;

/// <summary>An extra workspace window: its own workspace, name and bounds.</summary>
public class WorkspaceWindowDto : WorkspaceLayoutDto
{
	public string Id { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;

	/// <summary>Position in device pixels; null = centered on the screen.</summary>
	public double? X { get; set; }
	public double? Y { get; set; }
	public double Width { get; set; }
	public double Height { get; set; }
	public bool IsMaximized { get; set; }
}
