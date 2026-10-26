using System.Text.Json;
using LT.Contracts.Dtos.Cache;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Repository.Repositories;

/// <summary>
/// One JSON file per entry under &lt;app-data&gt;/cache/&lt;type&gt;/. A file's write time is the
/// entry's fetch time, which the size limit evicts by. Registered as a singleton. A cache is
/// disposable: unreadable files are misses, and failed writes or deletes are ignored.
/// </summary>
public class FileCacheRepository(string directory) : ICacheRepository
{
	private readonly Lock _lock = new();

	// Total bytes, kept up to date by Save so a write needn't walk the folder; null = unknown
	private long? _sizeBytes;

	public FileCacheRepository()
		: this(AppPathHelpers.GetCacheDirectory()) { }

	public CacheEntryEntity<T>? Get<T>(CacheType type, string key)
	{
		var path = GetPath(type, key);
		lock (_lock)
		{
			try
			{
				if (!File.Exists(path))
					return null;
				var entry = JsonSerializer.Deserialize<CacheEntryEntity<T>>(File.ReadAllText(path));
				// a hash collision or a hand-edited file is a miss, never another key's data
				return entry?.Key == key ? entry : null;
			}
			catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException)
			{
				return null;
			}
		}
	}

	public void Save<T>(CacheType type, CacheEntryEntity<T> entry)
	{
		var path = GetPath(type, entry.Key);
		lock (_lock)
		{
			try
			{
				var oldBytes = File.Exists(path) ? new FileInfo(path).Length : 0;
				Directory.CreateDirectory(Path.GetDirectoryName(path)!);
				File.WriteAllText(path, JsonSerializer.Serialize(entry));
				File.SetLastWriteTimeUtc(path, entry.FetchedAt.UtcDateTime);
				_sizeBytes += new FileInfo(path).Length - oldBytes;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				_sizeBytes = null;
			}
		}
	}

	public long GetSizeBytes()
	{
		lock (_lock)
			return _sizeBytes ??= ListFiles().Sum(x => x.SizeBytes);
	}

	public void Trim(long maxBytes)
	{
		lock (_lock)
		{
			if ((_sizeBytes ??= ListFiles().Sum(x => x.SizeBytes)) <= maxBytes)
				return;
			foreach (var file in CacheHelpers.SelectEvictions(ListFiles(), maxBytes))
				TryDelete(file.Path);
			_sizeBytes = null;
		}
	}

	public void Clear()
	{
		lock (_lock)
		{
			foreach (var file in ListFiles())
				TryDelete(file.Path);
			_sizeBytes = null;
		}
	}

	public void RemoveLegacyData()
	{
		var legacy = AppPathHelpers.GetLegacyBarCacheDirectory();
		try
		{
			if (Directory.Exists(legacy))
				Directory.Delete(legacy, recursive: true);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
	}

	private string GetPath(CacheType type, string key) =>
		Path.Combine(directory, type.ToString(), CacheHelpers.GetFileName(key));

	private List<CacheFileDto> ListFiles()
	{
		if (!Directory.Exists(directory))
			return [];
		var files = new List<CacheFileDto>();
		foreach (
			var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories)
		)
		{
			try
			{
				var info = new FileInfo(path);
				files.Add(
					new CacheFileDto
					{
						Path = path,
						SizeBytes = info.Length,
						WrittenAt = info.LastWriteTimeUtc,
					}
				);
			}
			catch (IOException) { }
		}
		return files;
	}

	private static void TryDelete(string path)
	{
		try
		{
			File.Delete(path);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
	}
}
