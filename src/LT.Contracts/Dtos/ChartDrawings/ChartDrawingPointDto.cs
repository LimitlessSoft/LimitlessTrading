namespace LT.Contracts.Dtos.ChartDrawings;

/// <summary>
/// An anchor of a chart drawing: a time and a price, so the drawing follows the bars through pan,
/// zoom, resize and new bars. Times past the last bar (or before the first) are extrapolated at
/// the timeframe's bar length.
/// </summary>
public class ChartDrawingPointDto
{
	public DateTimeOffset Time { get; set; }
	public double Price { get; set; }
}
