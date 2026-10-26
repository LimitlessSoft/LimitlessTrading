using LT.Contracts.Dtos.Charts;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

/// <summary>
/// Pre- / post-market bars are told apart by the contract's liquid hours from IB: the listed days
/// as listed, every other day by the usual week, in the exchange's zone (DST included).
/// </summary>
public class TradingHoursHelpersTests
{
	// SPY's liquidHours as IB sent them (Sat 24 – Mon 26 Oct 2026); New York is on EDT (UTC-4)
	private const string SpyLiquidHours =
		"20261024:CLOSED;20261025:CLOSED;20261026:0930-20261026:1600";

	private static readonly TimeSpan FiveMinutes = TimeSpan.FromMinutes(5);

	private static TradingScheduleDto Spy() =>
		TradingHoursHelpers.Parse(SpyLiquidHours, "US/Eastern")!;

	private static DateTimeOffset Utc(int month, int day, int hour, int minute = 0) =>
		new(2026, month, day, hour, minute, 0, TimeSpan.Zero);

	[Theory]
	[InlineData(8, 0, false)] // 04:00 ET pre-market
	[InlineData(13, 25, false)] // 09:25 ET, the last pre-market bar
	[InlineData(13, 30, true)] // 09:30 ET open
	[InlineData(19, 55, true)] // 15:55 ET, the last regular bar
	[InlineData(20, 0, false)] // 16:00 ET post-market
	public void A_listed_day_is_regular_from_the_open_to_the_close(
		int hour,
		int minute,
		bool regular
	) =>
		Assert.Equal(
			regular,
			TradingHoursHelpers.IsRegular(Spy(), Utc(10, 26, hour, minute), FiveMinutes)
		);

	[Fact]
	public void A_bar_holding_the_open_counts_as_regular() =>
		// 09:00–10:00 ET
		Assert.True(TradingHoursHelpers.IsRegular(Spy(), Utc(10, 26, 13), TimeSpan.FromHours(1)));

	[Theory]
	[InlineData(14, 25, false)] // 09:25 EST
	[InlineData(14, 30, true)] // 09:30 EST: winter shifts the open an hour in UTC
	[InlineData(20, 55, true)]
	[InlineData(21, 0, false)]
	public void A_day_in_history_takes_the_usual_session_in_its_own_offset(
		int hour,
		int minute,
		bool regular
	) =>
		// Wednesday 14 Jan 2026, EST (UTC-5)
		Assert.Equal(
			regular,
			TradingHoursHelpers.IsRegular(
				Spy(),
				new DateTimeOffset(2026, 1, 14, hour, minute, 0, TimeSpan.Zero),
				FiveMinutes
			)
		);

	[Fact]
	public void A_weekend_day_not_listed_open_has_no_regular_session() =>
		Assert.False(TradingHoursHelpers.IsRegular(Spy(), Utc(10, 17, 15), FiveMinutes));

	[Fact]
	public void A_listed_holiday_and_early_close_do_not_change_other_weeks()
	{
		var schedule = TradingHoursHelpers.Parse(
			"20261126:CLOSED;20261127:0930-20261127:1300;20261130:0930-20261130:1600;"
				+ "20261201:0930-20261201:1600",
			"US/Eastern"
		)!;

		// Thanksgiving (listed closed) and the half day after it; EST (UTC-5)
		Assert.False(TradingHoursHelpers.IsRegular(schedule, Utc(11, 26, 15), FiveMinutes));
		Assert.False(TradingHoursHelpers.IsRegular(schedule, Utc(11, 27, 18, 30), FiveMinutes));
		// The Thursday and Friday before keep the usual 09:30–16:00
		Assert.True(TradingHoursHelpers.IsRegular(schedule, Utc(11, 19, 15), FiveMinutes));
		Assert.True(TradingHoursHelpers.IsRegular(schedule, Utc(11, 20, 18, 30), FiveMinutes));
	}

	[Fact]
	public void A_session_past_midnight_counts_on_the_next_day()
	{
		// 18:00 to 17:00 the next day, Chicago (CDT, UTC-5)
		var schedule = TradingHoursHelpers.Parse(
			"20261025:1800-20261026:1700;20261026:1800-20261027:1700",
			"US/Central"
		)!;

		Assert.True(TradingHoursHelpers.IsRegular(schedule, Utc(10, 26, 8), FiveMinutes));
		Assert.False(TradingHoursHelpers.IsRegular(schedule, Utc(10, 26, 22, 30), FiveMinutes));
	}

	[Fact]
	public void The_older_format_with_times_only_is_read()
	{
		var schedule = TradingHoursHelpers.Parse("20261026:0930-1600", "US/Eastern")!;

		var session = Assert.Single(TradingHoursHelpers.GetSessions(schedule, new(2026, 10, 26)));
		Assert.Equal(
			(new TimeOnly(9, 30), TimeSpan.FromHours(6.5)),
			(session.Start, session.Length)
		);
	}

	[Theory]
	[InlineData("US/Eastern", "America/New_York")]
	[InlineData("US/Central", "America/Chicago")]
	[InlineData("Europe/Zurich", "Europe/Zurich")]
	public void Old_zone_names_ib_sends_are_found_on_every_system(string ibZone, string ianaZone) =>
		Assert.Equal(
			TimeZoneInfo.FindSystemTimeZoneById(ianaZone).GetUtcOffset(Utc(1, 14, 12)),
			TradingHoursHelpers.FindZone(ibZone)!.GetUtcOffset(Utc(1, 14, 12))
		);

	[Theory]
	[InlineData("", "US/Eastern")]
	[InlineData("20261024:CLOSED;20261025:CLOSED", "US/Eastern")]
	[InlineData("garbage", "US/Eastern")]
	[InlineData(SpyLiquidHours, "Not/A_Zone")]
	[InlineData(SpyLiquidHours, "")]
	public void Nothing_usable_is_no_schedule(string hours, string zone) =>
		Assert.Null(TradingHoursHelpers.Parse(hours, zone));
}
