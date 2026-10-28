using LT.Contracts.Entities;

namespace LT.Contracts.Interfaces.IRepositories;

/// <summary>The chart drawings of every contract and the last used style per kind (chart-drawings.json).</summary>
public interface IChartDrawingRepository
{
	/// <summary>An empty file when there is none or it can't be read.</summary>
	ChartDrawingFileEntity Get();

	void Save(ChartDrawingFileEntity file);
}
