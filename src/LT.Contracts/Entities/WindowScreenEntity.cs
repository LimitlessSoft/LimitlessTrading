namespace LT.Contracts.Entities;

/// <summary>
/// The screen a window was on when saved: its name and bounds (device pixels), so it can be found
/// again when the screens are listed in another order.
/// </summary>
public class WindowScreenEntity
{
	public string? Name { get; set; }
	public double X { get; set; }
	public double Y { get; set; }
	public double Width { get; set; }
	public double Height { get; set; }
}
