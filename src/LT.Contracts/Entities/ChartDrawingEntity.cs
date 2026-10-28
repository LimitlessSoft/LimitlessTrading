using LT.Contracts.Enums;

namespace LT.Contracts.Entities;

/// <summary>One chart drawing as saved in chart-drawings.json.</summary>
public class ChartDrawingEntity
{
	public string Id { get; set; } = string.Empty;
	public int ConId { get; set; }
	public ChartDrawingKind Kind { get; set; }
	public List<ChartDrawingPointEntity> Points { get; set; } = [];
	public string? Text { get; set; }
}
