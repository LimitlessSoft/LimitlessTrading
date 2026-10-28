using LT.Contracts.Enums;

namespace LT.Contracts.Entities;

/// <summary>The style last given to a drawing of one kind: new drawings of the kind start with it.</summary>
public class ChartDrawingToolStyleEntity
{
	public ChartDrawingKind Kind { get; set; }
	public ChartDrawingStyleEntity Style { get; set; } = new();
}
