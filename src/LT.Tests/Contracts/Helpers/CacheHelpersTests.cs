using LT.Contracts.Dtos.Cache;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class CacheHelpersTests
{
	private static readonly DateTimeOffset T0 = new(2026, 10, 23, 15, 0, 0, TimeSpan.Zero);

	private static CacheFileDto File(string path, long size, int minute) =>
		new()
		{
			Path = path,
			SizeBytes = size,
			WrittenAt = T0.AddMinutes(minute),
		};

	[Fact]
	public void Nothing_is_evicted_under_the_limit() =>
		Assert.Empty(CacheHelpers.SelectEvictions([File("a", 10, 0), File("b", 10, 1)], 20));

	[Fact]
	public void Oldest_files_go_until_the_rest_fits()
	{
		var evicted = CacheHelpers.SelectEvictions(
			[File("new", 10, 2), File("old", 10, 0), File("mid", 10, 1)],
			15
		);

		Assert.Equal(["old", "mid"], evicted.Select(x => x.Path));
	}

	[Fact]
	public void File_names_are_safe_and_stable()
	{
		var name = CacheHelpers.GetFileName("BRK B|STK/../x");

		Assert.Equal(name, CacheHelpers.GetFileName("BRK B|STK/../x"));
		Assert.DoesNotContain('/', name);
		Assert.EndsWith(".json", name);
		Assert.NotEqual(name, CacheHelpers.GetFileName("BRK A|STK"));
	}

	[Fact]
	public void A_type_needs_both_switches()
	{
		var settings = new AppSettingsDto
		{
			CacheEnabled = true,
			CacheChartBars = true,
			CacheSnapshots = false,
		};

		Assert.True(CacheHelpers.IsEnabled(settings, CacheType.ChartBars));
		Assert.False(CacheHelpers.IsEnabled(settings, CacheType.Snapshots));
		settings.CacheEnabled = false;
		Assert.False(CacheHelpers.IsEnabled(settings, CacheType.ChartBars));
	}

	[Theory]
	[InlineData(512, "512 B")]
	[InlineData(2048, "2 KB")]
	[InlineData(5 * 1024 * 1024, "5 MB")]
	public void Sizes_read_as_bytes_kb_or_mb(long bytes, string expected) =>
		Assert.Equal(expected, CacheHelpers.FormatSize(bytes));

	[Fact]
	public void Cached_today_shows_the_time_only() =>
		Assert.Equal("Cached 14:05", CacheHelpers.FormatCachedAt(T0.AddMinutes(-55), T0));

	[Fact]
	public void Cached_on_an_earlier_day_shows_the_date() =>
		Assert.Equal(
			$"Cached {T0.AddDays(-2):MMM d} 15:00",
			CacheHelpers.FormatCachedAt(T0.AddDays(-2), T0)
		);

	[Fact]
	public void Stale_banner_names_the_account_and_the_error() =>
		Assert.Equal(
			"Cached 14:05 · DU1 · not live — Timed out",
			CacheHelpers.FormatStale(T0.AddMinutes(-55), T0, "DU1", "Timed out")
		);
}
