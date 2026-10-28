using LT.Contracts.Entities;

namespace LT.Contracts.Interfaces.IRepositories;

/// <summary>The chart drawings of every contract (chart-drawings.json).</summary>
public interface IChartDrawingRepository
{
	/// <summary>Empty when there is no file or it can't be read.</summary>
	List<ChartDrawingEntity> Get();

	void Save(IReadOnlyList<ChartDrawingEntity> drawings);
}
