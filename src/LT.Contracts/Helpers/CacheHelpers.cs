using System.Security.Cryptography;
using System.Text;
using LT.Contracts.Dtos.Cache;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

public static class CacheHelpers
{
	/// <summary>A file-system-safe name for any key (symbols may hold '/', '.' and the like).</summary>
	public static string GetFileName(string key) =>
		Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".json";

	/// <summary>The oldest-written files to delete so the rest fit in <paramref name="maxBytes"/>.</summary>
	public static IReadOnlyList<CacheFileDto> SelectEvictions(
		IEnumerable<CacheFileDto> files,
		long maxBytes
	)
	{
		var ordered = files.OrderBy(x => x.WrittenAt).ToList();
		var total = ordered.Sum(x => x.SizeBytes);
		var evict = new List<CacheFileDto>();
		foreach (var file in ordered)
		{
			if (total <= maxBytes)
				break;
			evict.Add(file);
			total -= file.SizeBytes;
		}
		return evict;
	}

	/// <summary>The master switch and the type's own switch are both on.</summary>
	public static bool IsEnabled(AppSettingsDto settings, CacheType type) =>
		settings.CacheEnabled
		&& type switch
		{
			CacheType.ChartBars => settings.CacheChartBars,
			CacheType.Contracts => settings.CacheContracts,
			CacheType.OptionChains => settings.CacheOptionChains,
			CacheType.Executions => settings.CacheExecutions,
			CacheType.Snapshots => settings.CacheSnapshots,
			_ => false,
		};

	/// <summary>"12.3 MB" for the settings window.</summary>
	public static string FormatSize(long bytes) =>
		bytes < 1024 ? $"{bytes} B"
		: bytes < 1024 * 1024 ? $"{bytes / 1024.0:0.#} KB"
		: $"{bytes / (1024.0 * 1024):0.#} MB";

	/// <summary>Key of one kind of data of one account: accounts never share an entry.</summary>
	public static string GetAccountKey(string kind, string account) => $"{kind}|{account}";

	/// <summary>"Cached 14:05", with the date when it isn't from today (both in local time).</summary>
	public static string FormatCachedAt(DateTimeOffset fetchedAt, DateTimeOffset now) =>
		fetchedAt.Date == now.Date
			? $"Cached {fetchedAt:HH:mm}"
			: $"Cached {fetchedAt:MMM d HH:mm}";

	/// <summary>
	/// A widget's banner over last-known data: "Cached 14:05 · DU123 · not live — why". Local times.
	/// </summary>
	public static string FormatStale(
		DateTimeOffset fetchedAt,
		DateTimeOffset now,
		string account,
		string? error = null
	) =>
		$"{FormatCachedAt(fetchedAt, now)} · {account} · not live"
		+ (string.IsNullOrEmpty(error) ? string.Empty : $" — {error}");
}
