using LT.Contracts.Constants;
using LT.Contracts.Dtos.Account;
using LT.Contracts.Dtos.AccountPerformance;
using LT.Contracts.Entities;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>
/// Snapshot recording and the return math of the account performance widget.
///
/// Returns are built from IB's daily P&amp;L, not from net liquidation deltas: the socket API
/// doesn't report deposits and withdrawals, and a deposit would show up as a gain in a NetLiq
/// delta. A day's return is its daily P&amp;L over the equity it started with (net liquidation
/// minus the day's P&amp;L); a span's return chains those (time-weighted). Days without a recorded
/// daily P&amp;L are left out, as are days the app didn't run.
/// </summary>
public static class AccountPerformanceHelpers
{
	/// <summary>The trading day <paramref name="time"/> falls on: its date in <paramref name="zone"/>.</summary>
	public static DateOnly GetTradingDate(DateTimeOffset time, TimeZoneInfo zone) =>
		DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time, zone).DateTime);

	public static TimeZoneInfo GetTradingDayTimeZone() =>
		IbTimeHelpers.FindTimeZone(AccountPerformanceConstants.TradingDayTimeZoneId);

	/// <summary>
	/// Today's snapshot in <paramref name="days"/> (sorted by date), created when missing. A new
	/// day starts from <paramref name="carry"/>'s net liquidation and currency: the summary only
	/// arrives when one of its values changes, P&amp;L ticks don't wait for it.
	/// </summary>
	public static AccountDaySnapshotEntity GetOrAddDay(
		List<AccountDaySnapshotEntity> days,
		DateOnly date,
		DateTimeOffset now,
		AccountSummaryDto? carry
	)
	{
		var index = days.FindIndex(x => x.Date >= date);
		if (index >= 0 && days[index].Date == date)
			return days[index];

		var day = new AccountDaySnapshotEntity
		{
			Date = date,
			FirstUpdate = now,
			LastUpdate = now,
		};
		if (carry != null)
			ApplySummary(day, carry);
		days.Insert(index < 0 ? days.Count : index, day);
		return day;
	}

	/// <summary>Takes net liquidation and currency; values IB didn't send keep the previous ones.</summary>
	public static void ApplySummary(AccountDaySnapshotEntity day, AccountSummaryDto summary)
	{
		if (summary.NetLiquidation is { } netLiquidation)
			day.NetLiquidation = netLiquidation;
		if (summary.Currency.Length > 0)
			day.Currency = summary.Currency;
	}

	/// <summary>Takes the P&amp;L values; values IB didn't send keep the previous ones.</summary>
	public static void ApplyPnl(AccountDaySnapshotEntity day, AccountPnlDto pnl)
	{
		if (pnl.DailyPnl is { } daily)
			day.DailyPnl = daily;
		if (pnl.RealizedPnl is { } realized)
			day.RealizedPnl = realized;
		if (pnl.UnrealizedPnl is { } unrealized)
			day.UnrealizedPnl = unrealized;
	}

	/// <summary>
	/// The day's return as a fraction (0.01 = 1 %): daily P&amp;L over the equity at the start of
	/// the day. Null without both values or with no positive starting equity.
	/// </summary>
	public static double? GetDailyReturn(AccountDaySnapshotDto day)
	{
		if (day.DailyPnl is not { } pnl || day.NetLiquidation is not { } netLiquidation)
			return null;
		var start = (double)netLiquidation - pnl;
		return start > 0 ? pnl / start : null;
	}

	/// <summary>Summed P&amp;L and chained return of the days from <paramref name="from"/> to <paramref name="to"/>.</summary>
	public static PeriodReturnDto GetPeriodReturn(
		IEnumerable<AccountDaySnapshotDto> days,
		DateOnly from,
		DateOnly to
	)
	{
		double? pnl = null;
		double? growth = null;
		foreach (var day in days.Where(x => x.Date >= from && x.Date <= to))
		{
			if (day.DailyPnl is { } daily)
				pnl = (pnl ?? 0) + daily;
			if (GetDailyReturn(day) is { } r)
				growth = (growth ?? 1) * (1 + r);
		}
		return new PeriodReturnDto { Pnl = pnl, Percent = (growth - 1) * 100 };
	}

	/// <summary>Monday of the week <paramref name="date"/> is in.</summary>
	public static DateOnly GetWeekStart(DateOnly date) =>
		date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

	public static DateOnly GetMonthStart(DateOnly date) => new(date.Year, date.Month, 1);

	public static DateOnly GetYearStart(DateOnly date) => new(date.Year, 1, 1);

	/// <summary>First day shown for <paramref name="range"/>; null for all history.</summary>
	public static DateOnly? GetRangeStart(PerformanceRange range, DateOnly today) =>
		range switch
		{
			PerformanceRange.Week => today.AddDays(-6),
			PerformanceRange.Month => today.AddMonths(-1).AddDays(1),
			PerformanceRange.YearToDate => GetYearStart(today),
			_ => null,
		};

	/// <summary>The days on or after <paramref name="from"/> (all of them when null), oldest first.</summary>
	public static IReadOnlyList<AccountDaySnapshotDto> FilterRange(
		IEnumerable<AccountDaySnapshotDto> days,
		DateOnly? from
	) => days.Where(x => from == null || x.Date >= from).OrderBy(x => x.Date).ToList();

	/// <summary>
	/// Largest fall from a running peak, of the summed P&amp;L curve (money) and of the chained
	/// returns (percent). Both curves start at the beginning of the first day, so a losing first
	/// day already counts. Null values when no day has the needed data.
	/// </summary>
	public static DrawdownDto GetMaxDrawdown(IEnumerable<AccountDaySnapshotDto> days)
	{
		double? money = null;
		double? percent = null;
		double cumulative = 0,
			cumulativePeak = 0;
		double index = 1,
			indexPeak = 1;
		foreach (var day in days.OrderBy(x => x.Date))
		{
			if (day.DailyPnl is { } pnl)
			{
				cumulative += pnl;
				cumulativePeak = Math.Max(cumulativePeak, cumulative);
				money = Math.Min(money ?? 0, cumulative - cumulativePeak);
			}
			if (GetDailyReturn(day) is { } r)
			{
				index *= 1 + r;
				indexPeak = Math.Max(indexPeak, index);
				percent = Math.Min(percent ?? 0, (index / indexPeak - 1) * 100);
			}
		}
		return new DrawdownDto { Money = money, Percent = percent };
	}

	/// <summary>
	/// The month grid of <paramref name="month"/>: <see cref="AccountPerformanceConstants.CalendarWeeks"/>
	/// Monday-first weeks starting with the week of the 1st, each day with its recorded P&amp;L.
	/// </summary>
	public static IReadOnlyList<CalendarDayDto> BuildCalendarMonth(
		DateOnly month,
		IEnumerable<AccountDaySnapshotDto> days,
		DateOnly today
	)
	{
		var first = GetMonthStart(month);
		var start = GetWeekStart(first);
		var pnlByDate = days.Where(x => x.DailyPnl != null)
			.GroupBy(x => x.Date)
			.ToDictionary(x => x.Key, x => x.Last().DailyPnl);

		return Enumerable
			.Range(0, AccountPerformanceConstants.CalendarWeeks * 7)
			.Select(i =>
			{
				var date = start.AddDays(i);
				return new CalendarDayDto
				{
					Date = date,
					IsInMonth = date.Month == first.Month && date.Year == first.Year,
					IsToday = date == today,
					Pnl = pnlByDate.GetValueOrDefault(date),
				};
			})
			.ToList();
	}
}
