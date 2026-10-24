using System.Text.Json;
using LT.Contracts.Entities;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Repository.Repositories;

/// <summary>One JSON file per bar series in the app-data folder. Registered as a singleton.</summary>
public class BarCacheRepository : IBarCacheRepository
{
	private readonly Lock _lock = new();
	private readonly string _directory = AppPathHelpers.GetBarCacheDirectory();

	public BarCacheEntity? Get(string key)
	{
		var path = Path.Combine(_directory, $"{key}.json");
		lock (_lock)
		{
			if (!File.Exists(path))
				return null;

			try
			{
				return JsonSerializer.Deserialize<BarCacheEntity>(File.ReadAllText(path));
			}
			catch (JsonException)
			{
				return null;
			}
		}
	}

	public void Save(BarCacheEntity entity)
	{
		lock (_lock)
		{
			Directory.CreateDirectory(_directory);
			File.WriteAllText(
				Path.Combine(_directory, $"{entity.Key}.json"),
				JsonSerializer.Serialize(entity)
			);
		}
	}
}
