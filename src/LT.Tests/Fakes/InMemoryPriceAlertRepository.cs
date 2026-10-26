using LT.Contracts.Entities;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Tests.Fakes;

public class InMemoryPriceAlertRepository : IPriceAlertRepository
{
	public List<PriceAlertEntity> Alerts { get; set; } = [];
	public int SaveCount { get; private set; }

	public List<PriceAlertEntity> Get() => Alerts;

	public void Save(IReadOnlyList<PriceAlertEntity> alerts)
	{
		Alerts = [.. alerts];
		SaveCount++;
	}
}
