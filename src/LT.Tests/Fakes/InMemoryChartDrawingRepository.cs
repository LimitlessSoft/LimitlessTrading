using LT.Contracts.Entities;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Tests.Fakes;

public class InMemoryChartDrawingRepository : IChartDrawingRepository
{
	public List<ChartDrawingEntity> Drawings { get; set; } = [];
	public List<ChartDrawingToolStyleEntity> ToolStyles { get; set; } = [];
	public int SaveCount { get; private set; }

	public ChartDrawingFileEntity Get() => new() { Drawings = Drawings, ToolStyles = ToolStyles };

	public void Save(ChartDrawingFileEntity file)
	{
		Drawings = [.. file.Drawings];
		ToolStyles = [.. file.ToolStyles];
		SaveCount++;
	}
}
