namespace LT.Contracts.Dtos.ChartDrawings;

/// <summary>
/// A level of a Fibonacci retracement or extension: 0 and 1 are the move's ends (see
/// <c>ChartDrawingHelpers.GetFibRetracementPrice</c> / <c>GetFibExtensionPrice</c>).
/// </summary>
public class ChartDrawingFibLevelDto
{
	public double Value { get; set; }

	/// <summary>Drawn; a level switched off stays in the list to switch on again.</summary>
	public bool IsVisible { get; set; } = true;

	/// <summary>"#RRGGBB" or "#AARRGGBB"; null draws it in the drawing's colour.</summary>
	public string? Color { get; set; }
}
