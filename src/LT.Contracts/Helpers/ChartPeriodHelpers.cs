using LT.Contracts.Dtos.Charts;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>
/// A chart's period: a zoom preset, not a filter. It says where the view starts (the chart fits
/// the bars from there to now) and how much history to load so that start is covered. Rolling
/// periods go back from now; calendar periods start at midnight of the current day, week
/// (Monday) or month / year in the display time zone, the zone the chart draws its time axis in.
/// The end is always now.
/// </summary>
public static class ChartPeriodHelpers
{
	/// <summary>IB durations in days a request is rounded up to (fewer distinct cache entries).</summary>
	private static readonly int[] DurationDays = [2, 8, 32, 93, 185, 365];

	public static IReadOnlyList<ChartPeriod> RollingPeriods { get; } =
	[
		ChartPeriod.OneDay,
		ChartPeriod.SevenDays,
		ChartPeriod.OneMonth,
		ChartPeriod.ThreeMonths,
		ChartPeriod.SixMonths,
		ChartPeriod.OneYear,
		ChartPeriod.FiveYears,
	];

	public static IReadOnlyList<ChartPeriod> CalendarPeriods { get; } =
	[ChartPeriod.Today, ChartPeriod.ThisWeek, ChartPeriod.ThisMonth, ChartPeriod.ThisYear];

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
