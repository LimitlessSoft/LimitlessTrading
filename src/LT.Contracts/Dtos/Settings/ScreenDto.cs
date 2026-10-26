namespace LT.Contracts.Dtos.Settings;

/// <summary>A screen connected now, as the UI reports it.</summary>
public class ScreenDto
{
	public string? Name { get; set; }
	public ScreenRectDto Bounds { get; set; } = new();

	/// <summary>The bounds without the taskbar / dock / panels: where windows go.</summary>
	public ScreenRectDto WorkingArea { get; set; } = new();

	/// <summary>Device pixels per DIP.</summary>
	public double Scaling { get; set; } = 1;
	public bool IsPrimary { get; set; }
}
