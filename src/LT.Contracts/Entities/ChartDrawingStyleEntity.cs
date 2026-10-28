using LT.Contracts.Enums;

namespace LT.Contracts.Entities;

/// <summary>A drawing's colour and lines as saved in chart-drawings.json.</summary>
public class ChartDrawingStyleEntity
{
	public string Color { get; set; } = string.Empty;
	public ChartDrawingLineStyle LineStyle { get; set; }
	public double LineWidth { get; set; }
}
