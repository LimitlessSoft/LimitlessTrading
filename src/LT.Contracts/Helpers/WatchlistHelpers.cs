using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Watchlists;
using LT.Contracts.Entities;
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
