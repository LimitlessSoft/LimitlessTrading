using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Settings;

/// <summary>
/// Center layout tree node: a leaf holds one widget (<see cref="WidgetId"/>), a split arranges its
/// <see cref="Children"/> side by side or stacked. Edited in place by CenterLayoutHelpers.
/// </summary>
public class CenterNodeDto
{
	public string? WidgetId { get; set; }
	public SplitOrientation Orientation { get; set; }
	public List<CenterNodeDto> Children { get; set; } = [];

	/// <summary>Relative size within the parent split (star weight).</summary>
	public double Size { get; set; } = 1;

	public bool IsLeaf => WidgetId != null;
}
