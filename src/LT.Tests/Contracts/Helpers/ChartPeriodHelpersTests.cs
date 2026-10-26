using LT.Contracts.Dtos.Charts;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Contracts.Helpers;

public class ChartPeriodHelpersTests
{
	// Monday 26 Oct 2026, 14:30 UTC = 15:30 in Belgrade (CET, after the clocks went back)
	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 26, 14, 30, 0, TimeSpan.Zero)
	);

	private static readonly TimeZoneInfo Belgrade = TimeZoneInfo.FindSystemTimeZoneById(
		"Europe/Belgrade"
	);

	private DateTimeOffset Now => _time.GetUtcNow();

	[Fact]
	public void Rolling_periods_go_back_from_now()
	{
		Assert.Equal(
			Now.AddDays(-7),
			ChartPeriodHelpers.GetStart(ChartPeriod.SevenDays, Now, Belgrade)
		);
		Assert.Equal(
			Now.AddMonths(-3),
			ChartPeriodHelpers.GetStart(ChartPeriod.ThreeMonths, Now, Belgrade)
		);
		Assert.Equal(
			Now.AddYears(-1),
			ChartPeriodHelpers.GetStart(ChartPeriod.OneYear, Now, Belgrade)
		);
	}

	[Fact]
	public void Calendar_periods_start_at_midnight_in_the_zone()
	{
		var plusOne = TimeSpan.FromHours(1);
		Assert.Equal(
			new DateTimeOffset(2026, 10, 26, 0, 0, 0, plusOne),
			ChartPeriodHelpers.GetStart(ChartPeriod.Today, Now, Belgrade)
		);
		Assert.Equal(
			new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(2)),
			ChartPeriodHelpers.GetStart(ChartPeriod.ThisMonth, Now, Belgrade)
		);
		Assert.Equal(
			new DateTimeOffset(2026, 1, 1, 0, 0, 0, plusOne),
			ChartPeriodHelpers.GetStart(ChartPeriod.ThisYear, Now, Belgrade)
		);
	}

	[Fact]
	public void Today_follows_the_zone_date_not_utc()
	{
		// 23:30 UTC on the 26th is already the 27th in Belgrade
		_time.SetUtcNow(new DateTimeOffset(2026, 10, 26, 23, 30, 0, TimeSpan.Zero));

		Assert.Equal(
			new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.FromHours(1)),
			ChartPeriodHelpers.GetStart(ChartPeriod.Today, Now, Belgrade)
		);
	}

	[Theory]
	[InlineData(26, 26)] // Monday: the week started today
	[InlineData(28, 26)] // Wednesday
	[InlineData(25, 19)] // Sunday: still the week that started last Monday
	public void Weeks_start_on_monday(int day, int monday)
	{
		var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, day, 12, 0, 0, TimeSpan.Zero));

		var start = ChartPeriodHelpers.GetStart(
			ChartPeriod.ThisWeek,
			time.GetUtcNow(),
			TimeZoneInfo.Utc
		);

		Assert.Equal(new DateTimeOffset(2026, 10, monday, 0, 0, 0, TimeSpan.Zero), start);
	}

	[Fact]
	public void Auto_keeps_the_timeframe_duration_and_all_bars()
	{
		var range = ChartPeriodHelpers.GetRange(
			ChartPeriod.Auto,
			ChartTimeframe.FiveMinutes,
			Now,
			Belgrade
		);

		Assert.Equal(ChartTimeframeHelpers.GetDuration(ChartTimeframe.FiveMinutes), range.Duration);
		Assert.Null(range.From);
		Assert.Null(range.LimitedTo);
	}

	[Theory]
	[InlineData(ChartPeriod.OneDay, ChartTimeframe.FiveMinutes, "2 D")]
	[InlineData(ChartPeriod.SevenDays, ChartTimeframe.OneMinute, "8 D")]
	[InlineData(ChartPeriod.OneMonth, ChartTimeframe.FifteenMinutes, "32 D")]
	[InlineData(ChartPeriod.ThreeMonths, ChartTimeframe.OneHour, "93 D")]
	[InlineData(ChartPeriod.SixMonths, ChartTimeframe.OneDay, "185 D")]
	[InlineData(ChartPeriod.OneYear, ChartTimeframe.OneDay, "1 Y")]
	[InlineData(ChartPeriod.FiveYears, ChartTimeframe.OneWeek, "5 Y")]
	[InlineData(ChartPeriod.Today, ChartTimeframe.OneMinute, "2 D")]
	[InlineData(ChartPeriod.ThisMonth, ChartTimeframe.FiveMinutes, "32 D")]
	[InlineData(ChartPeriod.ThisYear, ChartTimeframe.OneDay, "365 D")]
	public void Duration_covers_the_period(
		ChartPeriod period,
		ChartTimeframe timeframe,
		string duration
	)
	{
		var range = ChartPeriodHelpers.GetRange(period, timeframe, Now, Belgrade);

		Assert.Equal(duration, range.Duration);
		Assert.Equal(ChartPeriodHelpers.GetStart(period, Now, Belgrade), range.From);
		Assert.Null(range.LimitedTo);
	}

	[Theory]
	[InlineData(ChartPeriod.OneMonth, ChartTimeframe.OneMinute, ChartPeriod.SevenDays, "8 D")]
	[InlineData(ChartPeriod.ThisYear, ChartTimeframe.FiveMinutes, ChartPeriod.OneMonth, "32 D")]
	[InlineData(
		ChartPeriod.OneYear,
		ChartTimeframe.FifteenMinutes,
		ChartPeriod.ThreeMonths,
		"93 D"
	)]
	[InlineData(ChartPeriod.FiveYears, ChartTimeframe.OneHour, ChartPeriod.OneYear, "1 Y")]
	public void Too_long_a_period_is_cut_to_the_timeframes_longest(
		ChartPeriod period,
		ChartTimeframe timeframe,
		ChartPeriod longest,
		string duration
	)
	{
		var range = ChartPeriodHelpers.GetRange(period, timeframe, Now, Belgrade);

		Assert.Equal(longest, range.LimitedTo);
		Assert.Equal(ChartPeriodHelpers.GetStart(longest, Now, Belgrade), range.From);
		Assert.Equal(duration, range.Duration);
	}

	[Fact]
	public void Period_shorter_than_a_bar_still_asks_for_a_few_bars()
	{
		Assert.Equal(
			"8 D",
			ChartPeriodHelpers
				.GetRange(ChartPeriod.Today, ChartTimeframe.OneDay, Now, Belgrade)
				.Duration
		);
		Assert.Equal(
			"32 D",
			ChartPeriodHelpers
				.GetRange(ChartPeriod.ThisWeek, ChartTimeframe.OneWeek, Now, Belgrade)
				.Duration
		);
	}

	[Fact]
	public void Trim_keeps_intraday_bars_ending_after_the_start()
	{
		var start = new DateTimeOffset(2026, 10, 26, 13, 0, 0, TimeSpan.Zero);
		IReadOnlyList<BarDto> bars =
		[
			Bar(start.AddHours(-2)),
			Bar(start.AddHours(-1)), // ends exactly at the start
			Bar(start.AddMinutes(-30)), // the start falls in it
			Bar(start.AddHours(1)),
		];

		var trimmed = ChartPeriodHelpers.Trim(bars, start, ChartTimeframe.OneHour, Belgrade);

		Assert.Equal([start.AddMinutes(-30), start.AddHours(1)], trimmed.Select(x => x.Time));
	}

	[Fact]
	public void Trim_compares_daily_bars_by_the_zone_date()
	{
		// Today in Belgrade starts at 23:00 UTC on the 25th; the 25th's daily bar is not today
		var today = ChartPeriodHelpers.GetStart(ChartPeriod.Today, Now, Belgrade)!.Value;
		IReadOnlyList<BarDto> bars =
		[
			Bar(new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero)),
			Bar(new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero)),
			Bar(new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero)),
		];

		var trimmed = ChartPeriodHelpers.Trim(bars, today, ChartTimeframe.OneDay, Belgrade);

		Assert.Equal(
			new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero),
			Assert.Single(trimmed).Time
		);
	}

	[Fact]
	public void Trim_returns_nothing_when_every_bar_is_older()
	{
		IReadOnlyList<BarDto> bars = [Bar(Now.AddDays(-3))];

		Assert.Empty(
			ChartPeriodHelpers.Trim(bars, Now.AddHours(-1), ChartTimeframe.OneMinute, Belgrade)
		);
	}

	private static BarDto Bar(DateTimeOffset time) =>
		new()
		{
			Time = time,
			Open = 1,
			High = 1,
			Low = 1,
			Close = 1,
		};
}
