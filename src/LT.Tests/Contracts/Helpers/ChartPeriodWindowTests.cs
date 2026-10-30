using LT.Contracts.Dtos.Charts;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

/// <summary>
/// A period is the window the chart shows: Today on intraday bars is the exchange's trading day
/// in the contract's sessions (regular for RTH, all for ETH), laid out whole with a slot per bar,
/// and bars before it are not part of it.
/// </summary>
public class ChartPeriodWindowTests
{
	// SPY around Friday 30 Oct 2026: New York on EDT (UTC-4), Belgrade on CET (UTC+1)
	private const string SpyLiquidHours =
		"20261030:0930-20261030:1600;20261031:CLOSED;20261101:CLOSED;20261102:0930-20261102:1600";
	private const string SpyTradingHours =
		"20261030:0400-20261030:2000;20261031:CLOSED;20261101:CLOSED;20261102:0400-20261102:2000";

	private static readonly TimeZoneInfo Belgrade = TimeZoneInfo.FindSystemTimeZoneById(
		"Europe/Belgrade"
	);

	// Friday 30 Oct, 13:55 in New York
	private DateTimeOffset _now = Utc(10, 30, 17, 55);

	private static readonly TradingScheduleDto Regular = TradingHoursHelpers.Parse(
		SpyLiquidHours,
		"US/Eastern"
	)!;
	private static readonly TradingScheduleDto Extended = TradingHoursHelpers.Parse(
		SpyTradingHours,
		"US/Eastern"
	)!;

	private static DateTimeOffset Utc(int month, int day, int hour, int minute = 0) =>
		new(2026, month, day, hour, minute, 0, TimeSpan.Zero);

	private ChartWindowDto Window(
		ChartPeriod period,
		TradingScheduleDto? sessions,
		ChartTimeframe timeframe = ChartTimeframe.FiveMinutes
	) => ChartPeriodHelpers.GetWindow(period, timeframe, _now, Belgrade, sessions)!;

	[Fact]
	public void Today_on_regular_hours_is_the_whole_regular_session()
	{
		var window = Window(ChartPeriod.Today, Regular);

		Assert.Equal(Utc(10, 30, 13, 30), window.Start);
		Assert.Equal(Utc(10, 30, 20, 0), window.End);
		var slots = window.SlotTimes!;
		Assert.Equal(78, slots.Count);
		Assert.Equal(Utc(10, 30, 13, 30), slots[0]);
		Assert.Equal(Utc(10, 30, 13, 35), slots[1]);
		Assert.Equal(Utc(10, 30, 19, 55), slots[^1]);
	}

	[Fact]
	public void Today_with_pre_and_post_market_is_the_whole_extended_session()
	{
		var window = Window(ChartPeriod.Today, Extended);

		Assert.Equal(Utc(10, 30, 8, 0), window.Start);
		Assert.Equal(Utc(10, 31, 0, 0), window.End);
		Assert.Equal(192, window.SlotTimes!.Count);
	}

	[Fact]
	public void The_previous_days_bars_are_not_in_today()
	{
		// Yesterday's last regular candle and last post-market candle (19:55 ET), which is after
		// midnight in Belgrade, then today's first two candles
		IReadOnlyList<BarDto> rth = [Bar(Utc(10, 29, 19, 55)), Bar(Utc(10, 30, 13, 30))];
		IReadOnlyList<BarDto> eth = [Bar(Utc(10, 29, 23, 55)), Bar(Utc(10, 30, 8, 0))];

		Assert.Equal(1, FirstIndex(rth, Window(ChartPeriod.Today, Regular)));
		Assert.Equal(1, FirstIndex(eth, Window(ChartPeriod.Today, Extended)));
		// Midnight in the display zone let the post-market candle in
		Assert.Equal(
			0,
			ChartPeriodHelpers.GetFirstIndex(
				eth,
				ChartPeriodHelpers.GetStart(ChartPeriod.Today, _now, Belgrade)!.Value,
				ChartTimeframe.FiveMinutes,
				Belgrade
			)
		);
	}

	[Fact]
	public void Before_the_open_today_is_still_todays_session()
	{
		// 03:00 in New York: yesterday's bars are all before it
		_now = Utc(10, 30, 7, 0);
		var window = Window(ChartPeriod.Today, Regular);

		Assert.Equal(Utc(10, 30, 13, 30), window.Start);
		Assert.Equal(1, FirstIndex([Bar(Utc(10, 29, 19, 55))], window));
	}

	[Fact]
	public void Today_on_a_closed_day_has_no_slots_and_starts_at_its_midnight()
	{
		_now = Utc(10, 31, 15, 0);
		var window = Window(ChartPeriod.Today, Regular);

		Assert.Empty(window.SlotTimes!);
		Assert.Equal(Utc(10, 31, 4, 0), window.Start);
		Assert.Equal(Utc(11, 1, 4, 0), window.End);
	}

	[Fact]
	public void This_week_holds_every_regular_session_of_monday_to_friday()
	{
		// Wednesday: Monday and Tuesday are past, Thursday and Friday to come (the usual week)
		_now = Utc(10, 28, 15, 0);
		var window = Window(ChartPeriod.ThisWeek, Regular);

		Assert.Equal(Utc(10, 26, 13, 30), window.Start);
		Assert.Equal(Utc(10, 30, 20, 0), window.End);
		Assert.Equal(5 * 78, window.SlotTimes!.Count);
	}

	[Fact]
	public void Hourly_slots_start_at_the_open_then_on_the_hour()
	{
		var slots = Window(ChartPeriod.Today, Regular, ChartTimeframe.OneHour).SlotTimes!;

		Assert.Equal([13, 14, 15, 16, 17, 18, 19], slots.Select(x => x.UtcDateTime.Hour).ToArray());
		Assert.Equal(30, slots[0].Minute);
		Assert.Equal(0, slots[1].Minute);
	}

	[Theory]
	[InlineData(15, 25)] // Monday 10:00 Chicago: in the session that opened Sunday 18:00
	[InlineData(23, 26)] // Monday 18:00 Chicago: the evening session is Tuesday's
	public void A_futures_evening_session_belongs_to_the_next_trading_day(int hour, int openDay)
	{
		var futures = TradingHoursHelpers.Parse(
			"20261025:1700-20261026:1600;20261026:1700-20261027:1600",
			"US/Central"
		)!;
		_now = Utc(10, 26, hour);

		var window = Window(ChartPeriod.Today, futures);

		// 17:00 Chicago (CDT, UTC-5)
		Assert.Equal(Utc(10, openDay, 22, 0), window.Start);
		Assert.Equal(23 * 12, window.SlotTimes!.Count);
	}

	[Fact]
	public void Daily_bars_count_trading_days_at_their_calendar_dates()
	{
		var week = Window(ChartPeriod.ThisWeek, null, ChartTimeframe.OneDay);
		var today = Window(ChartPeriod.Today, null, ChartTimeframe.OneDay);

		Assert.Equal([26, 27, 28, 29, 30], week.SlotTimes!.Select(x => x.Day).ToArray());
		Assert.All(week.SlotTimes!, x => Assert.Equal(TimeSpan.Zero, x.Offset));
		Assert.Equal(Utc(10, 30, 0, 0), Assert.Single(today.SlotTimes!));
		Assert.Equal(Utc(10, 31, 0, 0), today.End);
	}

	[Theory]
	[InlineData(ChartPeriod.OneDay, ChartTimeframe.FiveMinutes)]
	[InlineData(ChartPeriod.TwoDays, ChartTimeframe.OneMinute)]
	[InlineData(ChartPeriod.SevenDays, ChartTimeframe.OneHour)]
	[InlineData(ChartPeriod.ThisYear, ChartTimeframe.FiveMinutes)] // cut to a month
	public void Rolling_and_cut_periods_end_at_the_latest_bar(
		ChartPeriod period,
		ChartTimeframe timeframe
	)
	{
		var window = Window(period, Regular, timeframe);

		Assert.Equal(
			ChartPeriodHelpers.GetRange(period, timeframe, _now, Belgrade).From,
			window.Start
		);
		Assert.Null(window.End);
		Assert.Null(window.SlotTimes);
	}

	[Fact]
	public void Without_hours_today_starts_at_midnight_in_the_zone_and_ends_at_the_latest_bar()
	{
		var window = Window(ChartPeriod.Today, null);

		Assert.Equal(ChartPeriodHelpers.GetStart(ChartPeriod.Today, _now, Belgrade), window.Start);
		Assert.Null(window.SlotTimes);
	}

	[Fact]
	public void Auto_has_no_window()
	{
		Assert.Null(
			ChartPeriodHelpers.GetWindow(
				ChartPeriod.Auto,
				ChartTimeframe.FiveMinutes,
				_now,
				Belgrade,
				Regular
			)
		);
	}

	[Fact]
	public void Slots_after_the_latest_bar_follow_the_window_over_the_night()
	{
		_now = Utc(10, 26, 19, 0);
		var slots = Window(ChartPeriod.ThisWeek, Regular).SlotTimes;
		IReadOnlyList<BarDto> bars = [Bar(Utc(10, 26, 19, 50)), Bar(Utc(10, 26, 19, 55))];
		var five = TimeSpan.FromMinutes(5);

		Assert.Equal(Utc(10, 26, 19, 55), ChartPeriodHelpers.GetSlotTime(bars, 1, slots, five));
		Assert.Equal(Utc(10, 27, 13, 30), ChartPeriodHelpers.GetSlotTime(bars, 2, slots, five));
		Assert.Equal(Utc(10, 27, 13, 35), ChartPeriodHelpers.GetSlotTime(bars, 3, slots, five));
		// Without slots, a bar length apart
		Assert.Equal(Utc(10, 26, 20, 5), ChartPeriodHelpers.GetSlotTime(bars, 3, null, five));
	}

	private static int FirstIndex(IReadOnlyList<BarDto> bars, ChartWindowDto window) =>
		ChartPeriodHelpers.GetFirstIndex(bars, window.Start, ChartTimeframe.FiveMinutes, Belgrade);

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
