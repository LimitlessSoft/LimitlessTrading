using System.Globalization;
using LT.App.Helpers;
using LT.Contracts.Helpers;

namespace LT.Tests.WidgetsCore;

public class FormatHelpersTests
{
	private static readonly TimeZoneInfo Tokyo = DisplayTimeHelpers.ResolveZone("Asia/Tokyo");
	private static readonly TimeZoneInfo NewYork = DisplayTimeHelpers.ResolveZone(
		"America/New_York"
	);

	// 16:00 UTC is 01:00 the next day in Tokyo, 12:00 the same day in New York
	private static readonly DateTimeOffset Time = new(2026, 10, 26, 16, 0, 0, TimeSpan.Zero);

	[Fact]
	public void Time_is_in_the_given_zone() =>
		Assert.Equal("2026-10-27 01:00:00", FormatHelpers.Time(Time, Tokyo));

	[Fact]
	public void Day_time_counts_today_in_the_zone_not_in_utc()
	{
		var now = Time.AddHours(1); // still 26 Oct in UTC, 27 Oct in Tokyo

		Assert.Equal("01:00", FormatHelpers.DayTime(Time, now, Tokyo));
	}

	[Fact]
	public void Day_time_shows_the_date_when_the_zone_has_moved_to_another_day()
	{
		var now = Time.AddHours(9); // 01:00 UTC on 27 Oct: 27 Oct in UTC, 26 Oct 21:00 in New York

		Assert.Equal("12:00", FormatHelpers.DayTime(Time, now, NewYork));
		Assert.Equal(
			"2026-10-27 01:00",
			FormatHelpers.DayTime(Time, now.AddDays(1), Tokyo, otherDayFormat: "yyyy-MM-dd HH:mm")
		);
	}

	[Fact]
	public void Day_time_shows_the_year_only_for_another_year_when_asked()
	{
		const string day = "MM-dd HH:mm";
		const string year = "yyyy-MM-dd HH:mm";

		Assert.Equal(
			"06-26 16:00",
			FormatHelpers.DayTime(Time.AddMonths(-4), Time, TimeZoneInfo.Utc, "HH:mm", day, year)
		);
		Assert.Equal(
			"2025-08-28 12:00",
			FormatHelpers.DayTime(
				new DateTimeOffset(2025, 8, 28, 12, 0, 0, TimeSpan.Zero),
				Time,
				TimeZoneInfo.Utc,
				"HH:mm",
				day,
				year
			)
		);
		Assert.Equal(
			"08-28 12:00",
			FormatHelpers.DayTime(
				new DateTimeOffset(2025, 8, 28, 12, 0, 0, TimeSpan.Zero),
				Time,
				TimeZoneInfo.Utc,
				"HH:mm",
				day
			)
		);
	}

	[Fact]
	public void Market_cap_is_compact()
	{
		var culture = CultureInfo.CurrentCulture;
		CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
		try
		{
			Assert.Equal("2.41T", FormatHelpers.MarketCap(2_411_000_000_000));
			Assert.Equal("182.3B", FormatHelpers.MarketCap(182_310_000_000));
			Assert.Equal("950.2M", FormatHelpers.MarketCap(950_200_000));
			Assert.Equal("12,345", FormatHelpers.MarketCap(12_345));
			Assert.Equal(FormatHelpers.Empty, FormatHelpers.MarketCap(null));
		}
		finally
		{
			CultureInfo.CurrentCulture = culture;
		}
	}
}
