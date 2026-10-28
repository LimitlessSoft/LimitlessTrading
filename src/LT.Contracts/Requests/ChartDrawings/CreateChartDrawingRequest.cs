using LT.Contracts.Dtos.ChartDrawings;
using LT.Contracts.Enums;

namespace LT.Contracts.Requests.ChartDrawings;

/// <summary>A new drawing on the charts of a contract.</summary>
public class CreateChartDrawingRequest
{
	public int ConId { get; set; }
	public ChartDrawingKind Kind { get; set; }
	public List<ChartDrawingPointDto> Points { get; set; } = [];

	/// <summary>Required for <see cref="ChartDrawingKind.Text"/>, ignored for the others.</summary>
	public string? Text { get; set; }
}
