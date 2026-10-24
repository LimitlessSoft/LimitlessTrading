using LT.Contracts.Entities;

namespace LT.Contracts.Interfaces.IRepositories;

public interface IBarCacheRepository
{
	BarCacheEntity? Get(string key);
	void Save(BarCacheEntity entity);
}
