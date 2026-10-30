using LT.Contracts.Dtos.Settings;

namespace LT.Contracts.Helpers;

/// <summary>The per-contract settings list (Settings → General → Ticker settings).</summary>
public static class TickerSettingsHelpers
{
	/// <summary>The entry of a contract (by conId); null when it has none or has no conId.</summary>
	public static TickerSettingsDto? Find(IEnumerable<TickerSettingsDto> settings, int? conId) =>
		conId is > 0 and var id ? settings.FirstOrDefault(x => x.Contract?.ConId == id) : null;

	/// <summary>The second entry of a conId that already has one; null when every conId is unique.</summary>
	public static TickerSettingsDto? FindDuplicate(IEnumerable<TickerSettingsDto> settings)
	{
		var seen = new HashSet<int>();
		return settings.FirstOrDefault(x =>
			x.Contract is { } contract && !seen.Add(contract.ConId)
		);
	}
}
