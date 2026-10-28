using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.ChartDrawings;

/// <summary>A drawing on the charts of one contract (trend line, fib levels, a note…).</summary>
public class ChartDrawingDto
{
	public string Id { get; set; } = string.Empty;

	/// <summary>The contract (ConId) whose charts show it.</summary>
	public int ConId { get; set; }

	public ChartDrawingKind Kind { get; set; }

	/// <summary>Its anchors, as many as the kind takes (<c>ChartDrawingHelpers.GetPointCount</c>).</summary>
	public List<ChartDrawingPointDto> Points { get; set; } = [];

	/// <summary>The note of a <see cref="ChartDrawingKind.Text"/> drawing; null for the others.</summary>
	public string? Text { get; set; }
}
