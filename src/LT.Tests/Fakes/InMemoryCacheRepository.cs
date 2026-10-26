using System.Text.Json;
using LT.Contracts.Dtos.Cache;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Tests.Fakes;

/// <summary>
/// Entries kept as JSON, so tests get copies (like the file cache) and real sizes. Locked like the
/// file cache: symbol searches save from several tasks at once.
/// </summary>
public class InMemoryCacheRepository : ICacheRepository
{
	private readonly Lock _lock = new();

	public Dictionary<
		(CacheType Type, string Key),
		(string Json, DateTimeOffset WrittenAt)
	> Entries { get; } = [];
	public bool LegacyRemoved { get; private set; }

	public CacheEntryEntity<T>? Get<T>(CacheType type, string key)
	{
		lock (_lock)
			return Entries.TryGetValue((type, key), out var entry)
				? JsonSerializer.Deserialize<CacheEntryEntity<T>>(entry.Json)
				: null;
	}

	public void Save<T>(CacheType type, CacheEntryEntity<T> entry)
	{
		lock (_lock)
			Entries[(type, entry.Key)] = (JsonSerializer.Serialize(entry), entry.FetchedAt);
	}

	public long GetSizeBytes()
	{
		lock (_lock)
			return Entries.Values.Sum(x => (long)x.Json.Length);
	}

	public void Trim(long maxBytes)
	{
		lock (_lock)
		{
			var files = Entries
				.Select(x => new CacheFileDto
				{
					Path = $"{x.Key.Type}/{x.Key.Key}",
					SizeBytes = x.Value.Json.Length,
					WrittenAt = x.Value.WrittenAt,
				})
				.ToList();
			foreach (var file in CacheHelpers.SelectEvictions(files, maxBytes))
				Entries.Remove(Entries.Keys.First(x => $"{x.Type}/{x.Key}" == file.Path));
		}
	}

	public void Clear()
	{
		lock (_lock)
			Entries.Clear();
	}

	public void RemoveLegacyData() => LegacyRemoved = true;
}
