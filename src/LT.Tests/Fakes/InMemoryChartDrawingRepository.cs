using LT.Contracts.Entities;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Tests.Fakes;

public class InMemoryChartDrawingRepository : IChartDrawingRepository
{
	public List<ChartDrawingEntity> Drawings { get; set; } = [];
	public int SaveCount { get; private set; }

	public List<ChartDrawingEntity> Get() => Drawings;

	public void Save(IReadOnlyList<ChartDrawingEntity> drawings)
	{
		Drawings = [.. drawings];
		SaveCount++;
	}
}
