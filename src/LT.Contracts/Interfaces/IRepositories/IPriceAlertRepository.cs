using LT.Contracts.Entities;

namespace LT.Contracts.Interfaces.IRepositories;

/// <summary>The price alerts, active and triggered (price-alerts.json).</summary>
public interface IPriceAlertRepository
{
	/// <summary>Empty when there is no file or it can't be read.</summary>
	List<PriceAlertEntity> Get();

	void Save(IReadOnlyList<PriceAlertEntity> alerts);
}
