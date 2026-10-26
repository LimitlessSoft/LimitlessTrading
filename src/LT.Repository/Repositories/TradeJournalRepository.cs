using System.Globalization;
using System.Text.Json;
using LT.Contracts.Constants;
using LT.Contracts.Entities;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Repository.Repositories;

/// <summary>
/// The trade journal as JSON in the app-data folder: <c>trade-journal/fills/yyyy-MM-dd.json</c>
/// and <c>trade-journal/pnl/yyyy-MM-dd.json</c>. IB only returns the current day's executions,
/// so these files are the only history; each is written to a temp file and moved over the old
/// one. Registered as a singleton.
/// </summary>
public class TradeJournalRepository : ITradeJournalRepository
{
	private const string DayFormat = "yyyy-MM-dd";
	private readonly Lock _lock = new();
	private readonly string _fillsDirectory;
	private readonly string _pnlDirectory;

	public TradeJournalRepository()
	{
		var root = Path.Combine(
			AppPathHelpers.GetAppDataDirectory(),
			TradeJournalConstants.DirectoryName
		);
		_fillsDirectory = Path.Combine(root, TradeJournalConstants.FillsDirectoryName);
		_pnlDirectory = Path.Combine(root, TradeJournalConstants.PnlDirectoryName);
	}

	public IReadOnlyList<DateOnly> GetFillDays() => GetDays(_fillsDirectory);

	public IReadOnlyList<FillEntity> GetFills(DateOnly day) =>
		Read<FillEntity>(_fillsDirectory, day);

	public void SaveFills(DateOnly day, IReadOnlyList<FillEntity> fills) =>
		Write(_fillsDirectory, day, fills);

	public IReadOnlyList<DateOnly> GetPnlDays() => GetDays(_pnlDirectory);

	public IReadOnlyList<PnlSampleEntity> GetPnlSamples(DateOnly day) =>
		Read<PnlSampleEntity>(_pnlDirectory, day);

	public void SavePnlSamples(DateOnly day, IReadOnlyList<PnlSampleEntity> samples) =>
		Write(_pnlDirectory, day, samples);

	private IReadOnlyList<DateOnly> GetDays(string directory)
	{
		lock (_lock)
		{
			if (!Directory.Exists(directory))
				return [];
			return Directory
				.EnumerateFiles(directory, "*.json")
				.Select(x =>
					DateOnly.TryParseExact(
						Path.GetFileNameWithoutExtension(x),
						DayFormat,
						CultureInfo.InvariantCulture,
						DateTimeStyles.None,
						out var day
					)
						? day
						: (DateOnly?)null
				)
				.OfType<DateOnly>()
				.Order()
				.ToList();
		}
	}

	private List<T> Read<T>(string directory, DateOnly day)
	{
		var path = GetPath(directory, day);
		lock (_lock)
		{
			if (!File.Exists(path))
				return [];
			try
			{
				return JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path)) ?? [];
			}
			catch (JsonException)
			{
				// Kept aside rather than overwritten by the next save
				File.Move(path, path + ".unreadable", overwrite: true);
				return [];
			}
		}
	}

	private void Write<T>(string directory, DateOnly day, IReadOnlyList<T> items)
	{
		var path = GetPath(directory, day);
		lock (_lock)
		{
			Directory.CreateDirectory(directory);
			var temp = path + ".tmp";
			File.WriteAllText(temp, JsonSerializer.Serialize(items));
			File.Move(temp, path, overwrite: true);
		}
	}

	private static string GetPath(string directory, DateOnly day) =>
		Path.Combine(directory, day.ToString(DayFormat, CultureInfo.InvariantCulture) + ".json");
}
