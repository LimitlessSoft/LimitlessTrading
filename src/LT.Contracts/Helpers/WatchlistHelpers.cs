using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Watchlists;
using LT.Contracts.Entities;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Contracts.Helpers;

public static class WatchlistHelpers
{
	/// <summary>
	/// Makes sure there is at least one watchlist and an active one: settings from before named
	/// watchlists get theirs moved into "Main". True when the settings changed.
	/// </summary>
	public static bool EnsureWatchlists(SettingsEntity settings)
	{
		var changed = false;
		if (settings.Watchlists.Count == 0)
		{
			settings.Watchlists.Add(
				new WatchlistEntity
				{
					Name = MarketDataConstants.DefaultWatchlistName,
					Items = [.. settings.Watchlist],
				}
			);
			changed = true;
		}
		if (settings.Watchlist.Count > 0)
		{
			settings.Watchlist.Clear();
			changed = true;
		}
		if (!settings.Watchlists.Any(x => x.Id == settings.ActiveWatchlistId))
		{
			settings.ActiveWatchlistId = settings.Watchlists[0].Id;
			changed = true;
		}
		return changed;
	}

	/// <summary>
	/// Settings of a fresh install (no settings file yet): the defaults plus a "US" watchlist of
	/// starter stocks. Settings files without watchlists keep the empty "Main" of
	/// <see cref="EnsureWatchlists"/>, so a list migrated from older settings is never mixed with
	/// these.
	/// </summary>
	public static SettingsEntity CreateFirstRunSettings() =>
		new()
		{
			Watchlists =
			[
				new WatchlistEntity
				{
					Name = MarketDataConstants.StarterWatchlistName,
					Items = ContractHelpers.GetStarterContracts(),
				},
			],
		};

	/// <summary>Settings with a watchlist and an active one; older settings are migrated and saved.</summary>
	public static SettingsEntity LoadSettings(ISettingsRepository settingsRepository)
	{
		var settings = settingsRepository.Get();
		if (EnsureWatchlists(settings))
			settingsRepository.Save(settings);
		return settings;
	}

	public static WatchlistEntity GetActive(SettingsEntity settings) =>
		settings.Watchlists.First(x => x.Id == settings.ActiveWatchlistId);

	/// <summary>The watchlist with this id; null when it doesn't exist (deleted meanwhile).</summary>
	public static WatchlistEntity? Find(SettingsEntity settings, string id) =>
		settings.Watchlists.FirstOrDefault(x => x.Id == id);

	/// <summary>
	/// The watchlist a watchlist widget shows: <paramref name="id"/> while it still exists, else
	/// the active one (<paramref name="activeId"/>).
	/// </summary>
	public static string ResolveShownId(
		string? id,
		string activeId,
		IEnumerable<WatchlistDto> watchlists
	) => id != null && watchlists.Any(x => x.Id == id) ? id : activeId;

	/// <summary>The same listings (<see cref="ContractHelpers.IsSameListing"/>) in the same order.</summary>
	public static bool IsSameItems(IEnumerable<ContractDto> left, IEnumerable<ContractDto> right)
	{
		var a = left.ToList();
		var b = right.ToList();
		return a.Count == b.Count
			&& a.Zip(b).All(x => ContractHelpers.IsSameListing(x.First, x.Second));
	}

	/// <summary>Whether a pin is this listing's (conId and exchange, as <see cref="ContractHelpers.IsSameListing"/>).</summary>
	public static bool IsPinOf(WatchlistPinEntity pin, ContractDto contract) =>
		pin.ConId == contract.ConId
		&& string.Equals(pin.Exchange, contract.Exchange, StringComparison.OrdinalIgnoreCase);

	public static WatchlistPinEntity CreatePin(ContractDto contract) =>
		new() { ConId = contract.ConId, Exchange = contract.Exchange };

	/// <summary>Whether a custom column's cell is this listing's (conId and exchange).</summary>
	public static bool IsCellOf(WatchlistCellEntity cell, ContractDto contract) =>
		cell.ConId == contract.ConId
		&& string.Equals(cell.Exchange, contract.Exchange, StringComparison.OrdinalIgnoreCase);

	public static WatchlistCellEntity CreateCell(ContractDto contract, string value) =>
		new()
		{
			ConId = contract.ConId,
			Exchange = contract.Exchange,
			Value = value,
		};

	/// <summary>This listing's value in a custom column; empty when it has none.</summary>
	public static string GetCellValue(WatchlistColumnDto column, ContractDto contract) =>
		column
			.Cells.FirstOrDefault(x =>
				x.ConId == contract.ConId
				&& string.Equals(x.Exchange, contract.Exchange, StringComparison.OrdinalIgnoreCase)
			)
			?.Value
		?? string.Empty;

	/// <summary>Whether two column lists have the same columns (id and name) in the same order.</summary>
	public static bool IsSameColumns(
		IReadOnlyList<WatchlistColumnDto> left,
		IReadOnlyList<(string Id, string Name)> right
	) =>
		left.Count == right.Count
		&& left.Zip(right).All(x => x.First.Id == x.Second.Id && x.First.Name == x.Second.Name);

	/// <summary>Why a custom column name can't be used (blank, too long); null when it can.</summary>
	public static string? GetColumnNameError(string? name) =>
		string.IsNullOrWhiteSpace(name) ? WatchlistValidationCodes.WVC_009.GetValidationMessage()
		: name.Trim().Length > WatchlistConstants.ColumnNameMaxLength
			? WatchlistValidationCodes.WVC_010.GetValidationMessage(
				WatchlistConstants.ColumnNameMaxLength
			)
		: null;

	/// <summary>Why a custom column value can't be stored (too long); null when it can.</summary>
	public static string? GetCellValueError(string? value) =>
		(value?.Trim().Length ?? 0) > WatchlistConstants.CellValueMaxLength
			? WatchlistValidationCodes.WVC_014.GetValidationMessage(
				WatchlistConstants.CellValueMaxLength
			)
			: null;

	/// <summary>The first free "Column N" (N from 1).</summary>
	public static string GetNewColumnName(IEnumerable<string> existing)
	{
		var names = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
		for (var n = 1; ; n++)
		{
			var name = $"{WatchlistConstants.NewColumnNamePrefix} {n}";
			if (!names.Contains(name))
				return name;
		}
	}

	/// <summary>
	/// <paramref name="name"/> if no watchlist has it (case-insensitive), else the first free
	/// "name (2)", "name (3)" …, cut to fit the longest watchlist name.
	/// </summary>
	public static string GetFreeName(string name, IEnumerable<string> existing)
	{
		var names = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
		name = name.Trim();
		if (!names.Contains(name))
			return name;
		for (var n = 2; ; n++)
		{
			var suffix = $" ({n})";
			var stem =
				name.Length + suffix.Length > MarketDataConstants.WatchlistNameMaxLength
					? name[..(MarketDataConstants.WatchlistNameMaxLength - suffix.Length)].TrimEnd()
					: name;
			var candidate = stem + suffix;
			if (!names.Contains(candidate))
				return candidate;
		}
	}

	/// <summary>The first free "Watchlist N" (N from 2: the first list is "Main").</summary>
	public static string GetNewName(IEnumerable<string> existing)
	{
		var names = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
		for (var n = 2; ; n++)
		{
			var name = $"{MarketDataConstants.NewWatchlistNamePrefix} {n}";
			if (!names.Contains(name))
				return name;
		}
	}
}
