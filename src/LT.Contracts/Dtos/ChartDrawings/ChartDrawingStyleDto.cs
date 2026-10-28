using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.ChartDrawings;

/// <summary>How a drawing looks: its colour, line style and line width.</summary>
public class ChartDrawingStyleDto
{
	/// <summary>"#RRGGBB" or "#AARRGGBB".</summary>
	public string Color { get; set; } = string.Empty;

	public ChartDrawingLineStyle LineStyle { get; set; }

	/// <summary>In DIPs, <c>ChartDrawingConstants.MinLineWidth</c> … <c>MaxLineWidth</c>.</summary>
	public double LineWidth { get; set; }
}
