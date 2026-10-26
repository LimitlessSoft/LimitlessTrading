namespace LT.Contracts.Dtos.Settings;

/// <summary>A rectangle on the desktop, in device pixels.</summary>
public class ScreenRectDto
{
	public double X { get; set; }
	public double Y { get; set; }
	public double Width { get; set; }
	public double Height { get; set; }
	public double Right => X + Width;
	public double Bottom => Y + Height;
}
