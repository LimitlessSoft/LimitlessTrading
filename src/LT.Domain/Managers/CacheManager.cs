using LSCore.Mapper.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Cache;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Domain.Managers;

public class CacheManager(
	ICacheRepository cacheRepository,
	ISettingsRepository settingsRepository,
	TimeProvider timeProvider
) : ICacheManager
{
	public bool IsEnabled(CacheType type) =>
		CacheHelpers.IsEnabled(
			settingsRepository.Get().General.ToMapped<GeneralSettingsEntity, AppSettingsDto>(),
			type
		);

	public CachedDto<T>? Get<T>(CacheType type, string key)
	{
		if (!IsEnabled(type))
			return null;
		var entry = cacheRepository.Get<T>(type, key);
		if (entry == null || entry.Value is not { } value)
			return null;
		return new CachedDto<T> { Value = value, FetchedAt = entry.FetchedAt };
	}

	public CachedDto<T>? GetStale<T>(CacheType type, string key)
	{
		var cached = Get<T>(type, key);
		if (cached != null)
			cached.IsStale = true;
		return cached;
	}

	public void Save<T>(CacheType type, string key, T value)
	{
		// Settings read once: streams (positions, account summary) save on every update
		var general = settingsRepository.Get().General;
		if (
			!CacheHelpers.IsEnabled(general.ToMapped<GeneralSettingsEntity, AppSettingsDto>(), type)
		)
			return;
		cacheRepository.Save(
			type,
			new CacheEntryEntity<T>
			{
				Key = key,
				FetchedAt = timeProvider.GetUtcNow(),
				Value = value,
			}
		);
		cacheRepository.Trim((long)general.CacheMaxSizeMb * CacheConstants.BytesPerMb);
	}

	public async Task<CachedDto<T>> GetOrFetchAsync<T>(
		CacheType type,
		string key,
		TimeSpan ttl,
		Func<CancellationToken, Task<T>> fetch,
		CancellationToken cancellationToken = default,
		Func<T, bool>? canCache = null
	)
	{
		var cached = Get<T>(type, key);
		var now = timeProvider.GetUtcNow();
		if (cached != null && now - cached.FetchedAt < ttl)
			return cached;

		try
		{
			var value = await fetch(cancellationToken);
			if (canCache?.Invoke(value) ?? true)
				Save(type, key, value);
			return new CachedDto<T> { Value = value, FetchedAt = now };
		}
		catch (Exception ex) when (cached != null && ex is not OperationCanceledException)
		{
			cached.IsStale = true;
			cached.Error = ex.Message;
			return cached;
		}
	}

	public long GetSizeBytes() => cacheRepository.GetSizeBytes();

	public void Clear() => cacheRepository.Clear();

	public void EnforceSizeLimit() =>
		cacheRepository.Trim(
			(long)settingsRepository.Get().General.CacheMaxSizeMb * CacheConstants.BytesPerMb
		);

	public void RunStartupMaintenance()
	{
		cacheRepository.RemoveLegacyData();
		EnforceSizeLimit();
	}
}
