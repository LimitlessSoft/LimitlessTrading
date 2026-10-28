namespace LT.Contracts.Entities;

/// <summary>An anchor of a chart drawing as saved in chart-drawings.json.</summary>
public class ChartDrawingPointEntity
{
	public DateTimeOffset Time { get; set; }
	public double Price { get; set; }
}
