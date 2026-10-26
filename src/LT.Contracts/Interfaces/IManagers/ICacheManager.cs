using LT.Contracts.Dtos.Cache;
using LT.Contracts.Enums;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>
/// The local data cache, honouring the Cache settings: a type that is switched off (or the whole
/// cache) is neither read nor written. Every write is followed by the size limit.
/// </summary>
public interface ICacheManager
{
	bool IsEnabled(CacheType type);

	/// <summary>The cached entry whatever its age, or null (also when the type is off).</summary>
	CachedDto<T>? Get<T>(CacheType type, string key);

	/// <summary>Like <see cref="Get{T}"/>, marked IsStale: a fallback for when live data is unavailable.</summary>
	CachedDto<T>? GetStale<T>(CacheType type, string key);

	/// <summary>Stores a value fetched now; no-op when the type is off.</summary>
	void Save<T>(CacheType type, string key, T value);

	/// <summary>
	/// The cached value while it is younger than <paramref name="ttl"/>, else <paramref name="fetch"/>
	/// (stored when <paramref name="canCache"/> allows). When fetching fails and a copy is cached,
	/// that copy is returned with IsStale set; without one the error propagates.
	/// </summary>
	Task<CachedDto<T>> GetOrFetchAsync<T>(
		CacheType type,
		string key,
		TimeSpan ttl,
		Func<CancellationToken, Task<T>> fetch,
		CancellationToken cancellationToken = default,
		Func<T, bool>? canCache = null
	);

	/// <summary>Bytes the cache takes on disk.</summary>
	long GetSizeBytes();

	void Clear();

	/// <summary>Evicts the least recently written entries over the configured maximum size.</summary>
	void EnforceSizeLimit();

	/// <summary>On start: drops storage of earlier versions and applies the size limit.</summary>
	void RunStartupMaintenance();
}
