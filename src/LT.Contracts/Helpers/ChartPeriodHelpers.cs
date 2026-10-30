using LT.Contracts.Dtos.Charts;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>
/// A chart's period: the window the chart shows (<see cref="GetWindow"/>; bars before it stay
/// loaded for history and indicators but are not drawn while the view is on the period) and how
/// much history to load so that its start is covered (<see cref="GetRange"/>). Rolling periods
/// go back from now; calendar periods start at midnight of the current day, week (Monday) or
/// month / year in the display time zone, the zone the chart draws its time axis in — or, on
/// intraday bars with the contract's trading hours, at the open of the exchange's trading day.
/// </summary>
public static class ChartPeriodHelpers
{
	/// <summary>IB durations in days a request is rounded up to (fewer distinct cache entries).</summary>
	private static readonly int[] DurationDays = [2, 3, 8, 32, 93, 185, 365];

	public static IReadOnlyList<ChartPeriod> RollingPeriods { get; } =
	[
		ChartPeriod.OneDay,
		ChartPeriod.TwoDays,
		ChartPeriod.SevenDays,
		ChartPeriod.OneMonth,
		ChartPeriod.ThreeMonths,
		ChartPeriod.SixMonths,
		ChartPeriod.OneYear,
		ChartPeriod.FiveYears,
	];

	public static IReadOnlyList<ChartPeriod> CalendarPeriods { get; } =
	[ChartPeriod.Today, ChartPeriod.ThisWeek, ChartPeriod.ThisMonth, ChartPeriod.ThisYear];

	/// <summary>
	/// Every period in the order the chart's menu lists them: Auto, the rolling ones shortest
	/// first, then the calendar ones (not the enum's order, which only ever grows at the end).
	/// </summary>
	public static IReadOnlyList<ChartPeriod> AllPeriods { get; } =
	[ChartPeriod.Auto, .. RollingPeriods, .. CalendarPeriods];

	public static bool IsCalendar(ChartPeriod period) => CalendarPeriods.Contains(period);

	/// <summary>
	/// What the header shows instead of the period once the view was zoomed or scrolled away from
	/// it (not a period: the chosen one is still the one saved).
	/// </summary>
	public const string CustomDisplayName = "Custom";

	public static string GetDisplayName(ChartPeriod period) =>
		period switch
		{
			ChartPeriod.OneDay => "1 day",
			ChartPeriod.TwoDays => "2 days",
			ChartPeriod.SevenDays => "7 days",
			ChartPeriod.OneMonth => "1 month",
			ChartPeriod.ThreeMonths => "3 months",
			ChartPeriod.SixMonths => "6 months",
			ChartPeriod.OneYear => "1 year",
			ChartPeriod.FiveYears => "5 years",
			ChartPeriod.Today => "Today",
			ChartPeriod.ThisWeek => "This week",
			ChartPeriod.ThisMonth => "This month",
			ChartPeriod.ThisYear => "This year",
			_ => "Auto",
		};

	/// <summary>
	/// Where <paramref name="period"/> starts at <paramref name="now"/>; null for
	/// <see cref="ChartPeriod.Auto"/>. Calendar periods start at midnight in
	/// <paramref name="zone"/>; weeks start on Monday.
	/// </summary>
	public static DateTimeOffset? GetStart(
		ChartPeriod period,
		DateTimeOffset now,
		TimeZoneInfo zone
	)
	{
		var today = DisplayTimeHelpers.ToZone(now, zone).Date;
		return period switch
		{
			ChartPeriod.OneDay => now.AddDays(-1),
			ChartPeriod.TwoDays => now.AddDays(-2),
			ChartPeriod.SevenDays => now.AddDays(-7),
			ChartPeriod.OneMonth => now.AddMonths(-1),
			ChartPeriod.ThreeMonths => now.AddMonths(-3),
			ChartPeriod.SixMonths => now.AddMonths(-6),
			ChartPeriod.OneYear => now.AddYears(-1),
			ChartPeriod.FiveYears => now.AddYears(-5),
			ChartPeriod.Today => GetMidnight(today, zone),
			ChartPeriod.ThisWeek => GetMidnight(
				today.AddDays(-(((int)today.DayOfWeek + 6) % 7)),
				zone
			),
			ChartPeriod.ThisMonth => GetMidnight(new DateTime(today.Year, today.Month, 1), zone),
			ChartPeriod.ThisYear => GetMidnight(new DateTime(today.Year, 1, 1), zone),
			_ => null,
		};
	}

	/// <summary>
	/// The longest period a timeframe loads (IB serves long spans of small bars slowly and they
	/// are thousands of bars); null when any period fits.
	/// </summary>
	public static ChartPeriod? GetLongestPeriod(ChartTimeframe timeframe) =>
		timeframe switch
		{
			ChartTimeframe.OneMinute => ChartPeriod.SevenDays,
			ChartTimeframe.FiveMinutes => ChartPeriod.OneMonth,
			ChartTimeframe.FifteenMinutes => ChartPeriod.ThreeMonths,
			ChartTimeframe.OneHour => ChartPeriod.OneYear,
			_ => null,
		};

	/// <summary>
	/// What to load for a period at a timeframe: at least the timeframe's own span (what Auto
	/// loads, so there is history to the left of the period), longer when the period needs it. A
	/// period longer than the timeframe's longest (<see cref="GetLongestPeriod"/>) is cut to it
	/// and says so in <see cref="ChartRangeDto.LimitedTo"/>. A period shorter than a bar still
	/// asks IB for a few bars (a week of daily, a month of weekly).
	/// </summary>
	public static ChartRangeDto GetRange(
		ChartPeriod period,
		ChartTimeframe timeframe,
		DateTimeOffset now,
		TimeZoneInfo zone
	)
	{
		var own = ChartTimeframeHelpers.GetDuration(timeframe);
		if (GetStart(period, now, zone) is not { } start)
			return new ChartRangeDto { Duration = own };

		ChartPeriod? limitedTo = null;
		if (
			GetLongestPeriod(timeframe) is { } longest
			&& GetStart(longest, now, zone) is { } earliest
			&& start < earliest
		)
		{
			start = earliest;
			limitedTo = longest;
		}

		var days = Math.Max((int)Math.Ceiling((now - start).TotalDays), GetMinimumDays(timeframe));
		return new ChartRangeDto
		{
			// The day of slack GetDuration adds must fit in the timeframe's own span too
			Duration =
				days + 1 <= ChartTimeframeHelpers.GetDurationDays(timeframe)
					? own
					: GetDuration(days),
			From = start,
			LimitedTo = limitedTo,
		};
	}

	/// <summary>
	/// IB durationStr covering <paramref name="days"/> back from now: a day of slack (IB counts
	/// days its own way), rounded up to a few fixed
	/// sizes; past a year in whole years.
	/// </summary>
	public static string GetDuration(int days)
	{
		foreach (var bucket in DurationDays)
			if (days + 1 <= bucket)
				return $"{bucket} D";
		return $"{(int)Math.Ceiling(days / 366.0)} Y";
	}

	/// <summary>
	/// Index of the first bar from <paramref name="from"/> on: the first that ends after it (the
	/// bar it falls in counts); <c>bars.Count</c> when every bar is older. Daily and weekly bars
	/// carry their calendar date at midnight UTC, so they are compared with
	/// <paramref name="from"/>'s date in <paramref name="zone"/>. Bars are oldest first.
	/// </summary>
	public static int GetFirstIndex(
		IReadOnlyList<BarDto> bars,
		DateTimeOffset from,
		ChartTimeframe timeframe,
		TimeZoneInfo zone
	)
	{
		var start = ChartTimeframeHelpers.IsIntraday(timeframe)
			? from
			: new DateTimeOffset(DisplayTimeHelpers.ToZone(from, zone).Date, TimeSpan.Zero);
		var length = ChartTimeframeHelpers.GetBarLength(timeframe);
		// Binary search: charts hold up to ChartConstants.MaxLoadedBars and ask on every render
		var low = 0;
		var high = bars.Count;
		while (low < high)
		{
			var middle = low + (high - low) / 2;
			if (bars[middle].Time + length <= start)
				low = middle + 1;
			else
				high = middle;
		}
		return low;
	}

	/// <summary>
	/// The window <paramref name="period"/> shows at <paramref name="now"/>: the chart draws only
	/// the bars from its start on, the older ones (loaded for history and indicator lookback)
	/// stay out of the view. Null for <see cref="ChartPeriod.Auto"/>.
	/// <para>
	/// Calendar periods are fixed windows (<see cref="ChartWindowDto.SlotTimes"/>): on intraday
	/// bars with the contract's <paramref name="sessions"/> (regular ones for RTH, all for ETH) the
	/// period is the exchange's trading days — Today is the trading day of the session running
	/// now (a futures evening session belongs to the next day), else today's date in the
	/// exchange's zone — from the open of its first session to the close of its last, a slot per
	/// bar of session time. Daily bars count trading days (Monday to Friday, or the days
	/// <paramref name="sessions"/> trades), weekly bars weeks, from the calendar period's start in
	/// <paramref name="zone"/>. Rolling periods, a calendar period cut to the timeframe's longest
	/// and intraday bars without sessions end at the latest bar (no slots).
	/// </para>
	/// </summary>
	public static ChartWindowDto? GetWindow(
		ChartPeriod period,
		ChartTimeframe timeframe,
		DateTimeOffset now,
		TimeZoneInfo zone,
		TradingScheduleDto? sessions
	)
	{
		var range = GetRange(period, timeframe, now, zone);
		if (range.From is not { } from)
			return null;
		if (!IsCalendar(period) || range.LimitedTo != null)
			return new ChartWindowDto { Start = from };
		if (!ChartTimeframeHelpers.IsIntraday(timeframe))
			return GetDayWindow(period, timeframe, from, now, zone, sessions);
		return sessions != null
			? GetSessionWindow(period, timeframe, now, sessions)
			: new ChartWindowDto { Start = from };
	}

	/// <summary>
	/// Time of bar slot <paramref name="index"/>: a bar's own time; after the latest bar the
	/// window's next slots (<see cref="ChartWindowDto.SlotTimes"/> after the latest bar's time, so
	/// nights and weekends are skipped), past them a bar length apart. Bars are oldest first and
	/// not empty.
	/// </summary>
	public static DateTimeOffset GetSlotTime(
		IReadOnlyList<BarDto> bars,
		int index,
		IReadOnlyList<DateTimeOffset>? slotTimes,
		TimeSpan barLength
	)
	{
		if (index < bars.Count)
			return bars[Math.Max(0, index)].Time;
		var after = index - bars.Count;
		if (slotTimes is { Count: > 0 })
		{
			// Binary search: the first slot after the latest bar
			var last = bars[^1].Time;
			var low = 0;
			var high = slotTimes.Count;
			while (low < high)
			{
				var middle = low + (high - low) / 2;
				if (slotTimes[middle] <= last)
					low = middle + 1;
				else
					high = middle;
			}
			if (low + after < slotTimes.Count)
				return slotTimes[low + after];
			if (low < slotTimes.Count)
				return slotTimes[^1] + barLength * (low + after - slotTimes.Count + 1);
		}
		return bars[^1].Time + barLength * (after + 1);
	}

	// Intraday: the sessions whose trading day is in the period, a slot per bar
	private static ChartWindowDto GetSessionWindow(
		ChartPeriod period,
		ChartTimeframe timeframe,
		DateTimeOffset now,
		TradingScheduleDto schedule
	)
	{
		var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, schedule.Zone).DateTime);
		var tradingDay = today;
		foreach (var session in GetSessions(schedule, today.AddDays(-1), today.AddDays(1)))
			if (session.Start <= now && now < session.End)
			{
				tradingDay = session.Day;
				break;
			}
		var (first, last) = GetDates(period, tradingDay);
		var sessions = GetSessions(schedule, first.AddDays(-1), last)
			.Where(x => x.Day >= first && x.Day <= last)
			.ToList();
		var length = ChartTimeframeHelpers.GetBarLength(timeframe);
		var slots = new SortedSet<DateTimeOffset>();
		foreach (var session in sessions)
		{
			// IB's first bar starts at the open, the next ones on the bar size's clock boundaries
			// (09:30, 10:00, 11:00… for hourly bars)
			var midnight = session.WallStart.Date;
			var wallEnd = session.WallStart + (session.End - session.Start);
			slots.Add(session.Start);
			var steps = (long)((session.WallStart - midnight).Ticks / length.Ticks) + 1;
			for (var wall = midnight.AddTicks(steps * length.Ticks); wall < wallEnd; wall += length)
				slots.Add(new DateTimeOffset(wall, schedule.Zone.GetUtcOffset(wall)));
		}
		return new ChartWindowDto
		{
			Start =
				sessions.Count > 0
					? sessions[0].Start
					: GetMidnight(first.ToDateTime(TimeOnly.MinValue), schedule.Zone),
			End =
				sessions.Count > 0
					? sessions.Max(x => x.End)
					: GetMidnight(last.AddDays(1).ToDateTime(TimeOnly.MinValue), schedule.Zone),
			SlotTimes = [.. slots],
		};
	}

	// Daily and weekly bars: trading days or weeks of the calendar period, at midnight UTC like
	// the bars' calendar dates
	private static ChartWindowDto GetDayWindow(
		ChartPeriod period,
		ChartTimeframe timeframe,
		DateTimeOffset from,
		DateTimeOffset now,
		TimeZoneInfo zone,
		TradingScheduleDto? schedule
	)
	{
		var (first, last) = GetDates(
			period,
			DateOnly.FromDateTime(DisplayTimeHelpers.ToZone(now, zone).DateTime)
		);
		var slots = new List<DateTimeOffset>();
		if (timeframe == ChartTimeframe.OneWeek)
			for (var monday = GetMonday(first); monday <= last; monday = monday.AddDays(7))
				slots.Add(ToUtcDate(monday));
		else
			for (var day = first; day <= last; day = day.AddDays(1))
				if (
					schedule != null
						? TradingHoursHelpers.GetSessions(schedule, day).Count > 0
						: day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
				)
					slots.Add(ToUtcDate(day));
		return new ChartWindowDto
		{
			Start = from,
			End = ToUtcDate(last.AddDays(1)),
			SlotTimes = slots,
		};
	}

	// First and last day of a calendar period holding the day
	private static (DateOnly First, DateOnly Last) GetDates(ChartPeriod period, DateOnly day) =>
		period switch
		{
			ChartPeriod.ThisWeek => (GetMonday(day), GetMonday(day).AddDays(6)),
			ChartPeriod.ThisMonth => (
				new DateOnly(day.Year, day.Month, 1),
				new DateOnly(day.Year, day.Month, 1).AddMonths(1).AddDays(-1)
			),
			ChartPeriod.ThisYear => (new DateOnly(day.Year, 1, 1), new DateOnly(day.Year, 12, 31)),
			_ => (day, day),
		};

	private static DateOnly GetMonday(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

	private static DateTimeOffset ToUtcDate(DateOnly day) =>
		new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

	// A session of the schedule: its trading day is the exchange date it closes on
	private readonly record struct Session(
		DateOnly Day,
		DateTime WallStart,
		DateTimeOffset Start,
		DateTimeOffset End
	);

	// The sessions listed under the days from … to, oldest first
	private static IEnumerable<Session> GetSessions(
		TradingScheduleDto schedule,
		DateOnly from,
		DateOnly to
	)
	{
		for (var day = from; day <= to; day = day.AddDays(1))
			foreach (
				var session in TradingHoursHelpers.GetSessions(schedule, day).OrderBy(x => x.Start)
			)
			{
				var wall = day.ToDateTime(session.Start);
				var start = new DateTimeOffset(wall, schedule.Zone.GetUtcOffset(wall));
				var end = start + session.Length;
				var closes = DateOnly.FromDateTime(
					TimeZoneInfo.ConvertTime(end.AddTicks(-1), schedule.Zone).DateTime
				);
				yield return new Session(closes, wall, start, end);
			}
	}

	private static int GetMinimumDays(ChartTimeframe timeframe) =>
		timeframe switch
		{
			ChartTimeframe.OneDay => 7,
			ChartTimeframe.OneWeek => 31,
			_ => 1,
		};

	// Midnight of a date in the zone; a midnight skipped by a clock change is the first valid time
	private static DateTimeOffset GetMidnight(DateTime date, TimeZoneInfo zone)
	{
		var time = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
		while (zone.IsInvalidTime(time))
			time = time.AddMinutes(15);
		return new DateTimeOffset(time, zone.GetUtcOffset(time));
	}
}
