namespace LT.Contracts.Entities;

/// <summary>chart-drawings.json.</summary>
public class ChartDrawingFileEntity
{
	public List<ChartDrawingEntity> Drawings { get; set; } = [];

	/// <summary>The last used style per kind (missing in files written before styles).</summary>
	public List<ChartDrawingToolStyleEntity> ToolStyles { get; set; } = [];
}
