using LT.Contracts.Enums;

namespace LT.Contracts.Entities;

/// <summary>
/// Center layout tree: a leaf holds one widget (<see cref="WidgetId"/>), a split arranges its
/// <see cref="Children"/> side by side or stacked.
/// </summary>
public class CenterNodeEntity
{
	public string? WidgetId { get; set; }
	public SplitOrientation Orientation { get; set; }
	public List<CenterNodeEntity> Children { get; set; } = [];

	/// <summary>Relative size within the parent split (star weight).</summary>
	public double Size { get; set; } = 1;
}
