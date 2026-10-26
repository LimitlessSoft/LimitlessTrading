using LT.Contracts.Entities;
using LT.Contracts.Enums;

namespace LT.Contracts.Interfaces.IRepositories;

/// <summary>Namespaced cache entries (one per type and key), whatever their age.</summary>
public interface ICacheRepository
{
	CacheEntryEntity<T>? Get<T>(CacheType type, string key);
	void Save<T>(CacheType type, CacheEntryEntity<T> entry);

	/// <summary>Total size of every entry, in bytes.</summary>
	long GetSizeBytes();

	/// <summary>Deletes the least recently written entries until the total fits <paramref name="maxBytes"/>.</summary>
	void Trim(long maxBytes);

	void Clear();

	/// <summary>Deletes storage of earlier versions (the old bar-cache folder).</summary>
	void RemoveLegacyData();
}
