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

	/// <summary>Its colour and lines; a drawing saved before styles has the kind's built-in one.</summary>
	public ChartDrawingStyleDto Style { get; set; } = new();

	/// <summary>Can be selected (to unlock it) but not moved, reshaped or deleted one by one.</summary>
	public bool IsLocked { get; set; }

	/// <summary>Not drawn and not hit; shown again from the chart's menu.</summary>
	public bool IsHidden { get; set; }

	/// <summary>A Fibonacci drawing's levels (the defaults until edited); empty for the others.</summary>
	public List<ChartDrawingFibLevelDto> FibLevels { get; set; } = [];
}
