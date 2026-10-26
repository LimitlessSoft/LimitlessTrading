using System.Text.Json;
using LT.Contracts.Constants;
using LT.Contracts.Entities;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Repository.Repositories;

/// <summary>
/// SEC share counts by ticker as one JSON file in the app-data folder (shares-outstanding.json),
/// read once and kept in memory. Unreadable or unwritable means looking up again: it is a cache.
/// Registered as a singleton.
/// </summary>
public class SharesOutstandingRepository : ISharesOutstandingRepository
{
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
	private readonly Lock _lock = new();
	private readonly string _path = Path.Combine(
		AppPathHelpers.GetAppDataDirectory(),
		SecConstants.SharesOutstandingFileName
	);
	private Dictionary<string, SharesOutstandingEntity>? _entries;

	public SharesOutstandingEntity? Get(string ticker)
	{
		lock (_lock)
			return Load().GetValueOrDefault(ticker);
	}

	public void Save(string ticker, SharesOutstandingEntity entry)
	{
		lock (_lock)
		{
			var entries = Load();
			entries[ticker] = entry;
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
				var temp = _path + ".tmp";
				File.WriteAllText(temp, JsonSerializer.Serialize(entries, JsonOptions));
				File.Move(temp, _path, overwrite: true);
			}
			catch (IOException) { }
			catch (UnauthorizedAccessException) { }
		}
	}

	private Dictionary<string, SharesOutstandingEntity> Load()
	{
		if (_entries != null)
			return _entries;
		try
		{
			_entries = File.Exists(_path)
				? JsonSerializer.Deserialize<Dictionary<string, SharesOutstandingEntity>>(
					File.ReadAllText(_path),
					JsonOptions
				)
				: null;
		}
		catch (Exception ex) when (ex is JsonException or IOException)
		{
			_entries = null;
		}
		return _entries ??= new Dictionary<string, SharesOutstandingEntity>(
			StringComparer.OrdinalIgnoreCase
		);
	}
}
