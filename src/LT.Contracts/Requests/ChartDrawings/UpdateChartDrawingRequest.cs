using LT.Contracts.Dtos.ChartDrawings;

namespace LT.Contracts.Requests.ChartDrawings;

/// <summary>A drawing's anchors were dragged or its note edited: its new points and note.</summary>
public class UpdateChartDrawingRequest
{
	public string Id { get; set; } = string.Empty;
	public List<ChartDrawingPointDto> Points { get; set; } = [];

	/// <summary>Required for a text drawing, ignored for the others.</summary>
	public string? Text { get; set; }
}
