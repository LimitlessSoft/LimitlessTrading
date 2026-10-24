using LT.Contracts.Entities;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Tests.Fakes;

public class InMemoryBarCacheRepository : IBarCacheRepository
{
	public Dictionary<string, BarCacheEntity> Entries { get; } = [];

	public BarCacheEntity? Get(string key) => Entries.GetValueOrDefault(key);

	public void Save(BarCacheEntity entity) => Entries[entity.Key] = entity;
}
