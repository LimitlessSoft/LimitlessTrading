using LT.Contracts.Dtos.ChartDrawings;

namespace LT.Contracts.Requests.ChartDrawings;

/// <summary>
/// A drawing's look or state changed (the style bar, its menu): every field left null stays as
/// it is.
/// </summary>
public class UpdateChartDrawingPropertiesRequest
{
	public string Id { get; set; } = string.Empty;

	/// <summary>Also becomes the style new drawings of its kind start with.</summary>
	public ChartDrawingStyleDto? Style { get; set; }

	public bool? IsLocked { get; set; }
	public bool? IsHidden { get; set; }

	/// <summary>Only for a Fibonacci drawing; ignored for the others.</summary>
	public List<ChartDrawingFibLevelDto>? FibLevels { get; set; }
}
