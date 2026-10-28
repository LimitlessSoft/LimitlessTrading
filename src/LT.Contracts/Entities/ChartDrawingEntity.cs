using LT.Contracts.Enums;

namespace LT.Contracts.Entities;

/// <summary>
/// One chart drawing as saved in chart-drawings.json. Files written before styles, lock / hide
/// and fib levels lack those fields: null style and levels are the kind's built-in ones.
/// </summary>
public class ChartDrawingEntity
{
	public string Id { get; set; } = string.Empty;
	public int ConId { get; set; }
	public ChartDrawingKind Kind { get; set; }
	public List<ChartDrawingPointEntity> Points { get; set; } = [];
	public string? Text { get; set; }

	/// <summary>Null: the kind's built-in style.</summary>
	public ChartDrawingStyleEntity? Style { get; set; }

	public bool IsLocked { get; set; }
	public bool IsHidden { get; set; }

	/// <summary>A Fibonacci drawing's levels; null: the kind's defaults.</summary>
	public List<ChartDrawingFibLevelEntity>? FibLevels { get; set; }
}
