namespace LT.Contracts.Entities;

/// <summary>A Fibonacci level as saved in chart-drawings.json.</summary>
public class ChartDrawingFibLevelEntity
{
	public double Value { get; set; }
	public bool IsVisible { get; set; } = true;
	public string? Color { get; set; }
}
