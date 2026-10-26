using LT.Contracts.Constants;
using LT.Contracts.Dtos.Account;
using LT.Contracts.Dtos.AccountPerformance;
using LT.Contracts.Dtos.Flex;
using LT.Contracts.Entities;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>
/// Snapshot recording, the Flex merge and the return math of the account performance widget.
///
/// Returns are built from each day's P&amp;L, never from raw net liquidation deltas, so deposits
/// and withdrawals don't count as gains: a recorded day uses IB's daily P&amp;L (which leaves cash
/// flows out); a Flex day uses its NAV change minus the day's net deposits and transfers
/// (<see cref="MergeFlexDays"/>). A day's return is its P&amp;L over the equity it started with
/// (net liquidation minus the day's P&amp;L, i.e. the previous close plus the day's deposits); a
/// span's return chains those (time-weighted). Days without P&amp;L are left out, as are days
/// with no data.
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

	/// <summary>
	/// Merges Flex end-of-day NAV into <paramref name="days"/> (sorted by date) for report dates
	/// before <paramref name="today"/> (today stays live): net liquidation, base currency and the
	/// day's net money moved in / out — deposits / withdrawals plus transfers (cash and
	/// positions), a transfer matching a deposit / withdrawal of the same date and amount counted
	/// once, as in <see cref="MergeCashMovements"/>; a movement on a date without NAV goes to the
	/// next NAV date — marked <see cref="AccountDaySource.Flex"/>. Then every Flex day's DailyPnl
	/// is recomputed as NAV − previous day's net liquidation − cash flow (null without a previous
	/// day). Recorded realized / unrealized P&amp;L are kept. Returns the days added or changed.
	/// </summary>
	public static int MergeFlexDays(
		List<AccountDaySnapshotEntity> days,
		IEnumerable<FlexNavDto> nav,
		IEnumerable<FlexCashFlowDto> cashFlows,
		IEnumerable<FlexCashFlowDto> transfers,
		DateOnly today,
		DateTimeOffset now
	)
	{
		var navByDate = nav.Where(x => x.Date < today)
			.GroupBy(x => x.Date)
			.ToDictionary(x => x.Key, x => x.Last());
		var navDates = navByDate.Keys.Order().ToList();
		var cash = cashFlows.ToList();
		var flows = cash.Concat(
			transfers.Where(t =>
				!cash.Exists(x => x.Date == t.Date && SameAmount(x.Amount, t.Amount))
			)
		);
		var flowByDate = new Dictionary<DateOnly, double>();
		foreach (var flow in flows)
		{
			var index = navDates.BinarySearch(flow.Date);
			if (index < 0)
				index = ~index;
			if (index >= navDates.Count)
				continue;
			var date = navDates[index];
			flowByDate[date] = flowByDate.GetValueOrDefault(date) + flow.Amount;
		}

		var changed = new HashSet<DateOnly>();
		foreach (var date in navDates)
		{
			var row = navByDate[date];
			var existed = days.Exists(x => x.Date == date);
			var day = GetOrAddDay(days, date, now, null);
			var cashFlow = flowByDate.GetValueOrDefault(date);
			var currency = row.Currency.Length > 0 ? row.Currency : day.Currency;
			if (
				existed
				&& day.NetLiquidation == row.Total
				&& day.Currency == currency
				&& day.CashFlow == cashFlow
				&& day.Source == AccountDaySource.Flex
			)
				continue;
			day.NetLiquidation = row.Total;
			day.Currency = currency;
			day.CashFlow = cashFlow;
			day.Source = AccountDaySource.Flex;
			day.LastUpdate = now;
			changed.Add(date);
		}

		decimal? previous = null;
		foreach (var day in days)
		{
			if (day.Source == AccountDaySource.Flex)
			{
				double? pnl =
					previous is { } start && day.NetLiquidation is { } end
						? (double)(end - start) - (day.CashFlow ?? 0)
						: null;
				if (day.DailyPnl != pnl)
				{
					day.DailyPnl = pnl;
					day.LastUpdate = now;
					changed.Add(day.Date);
				}
			}
			if (day.NetLiquidation is { } value)
				previous = value;
		}
		return changed.Count;
	}

	/// <summary>
	/// The dedupe key of a Flex deposit / withdrawal (<paramref name="isTransfer"/> false) or
	/// transfer: IB's transactionID when the query has it, else the row's date, kind, amount and
	/// description. Prefixed by section, so the two sections never collide.
	/// </summary>
	public static string GetCashMovementKey(FlexCashFlowDto flow, bool isTransfer)
	{
		var prefix = isTransfer ? "transfer:" : "cash:";
		return flow.TransactionId.Length > 0
			? prefix + flow.TransactionId
			: string.Create(
				System.Globalization.CultureInfo.InvariantCulture,
				$"{prefix}{flow.Date:yyyyMMdd}:{flow.Kind}:{flow.Amount:R}:{flow.Description}"
			);
	}

	/// <summary>
	/// Adds or updates the Flex deposits / withdrawals (<paramref name="cashFlows"/>) and
	/// transfers in <paramref name="movements"/> (kept sorted by date), by
	/// <see cref="GetCashMovementKey"/>; idempotent. A transfer row with the same date and amount
	/// as a deposit / withdrawal row is the same money seen in both sections and is skipped.
	/// Returns the movements added or changed.
	/// </summary>
	public static int MergeCashMovements(
		List<CashMovementEntity> movements,
		IEnumerable<FlexCashFlowDto> cashFlows,
		IEnumerable<FlexCashFlowDto> transfers
	)
	{
		var cash = cashFlows.ToList();
		var changed = 0;
		foreach (var flow in cash)
			changed += Upsert(movements, flow, GetCashMovementKey(flow, isTransfer: false));
		foreach (var flow in transfers)
		{
			var duplicate =
				cash.Exists(x => x.Date == flow.Date && SameAmount(x.Amount, flow.Amount))
				|| movements.Exists(x =>
					x.Id.StartsWith("cash:", StringComparison.Ordinal)
					&& x.Date == flow.Date
					&& SameAmount(x.Amount, flow.Amount)
				);
			if (!duplicate)
				changed += Upsert(movements, flow, GetCashMovementKey(flow, isTransfer: true));
		}
		if (changed > 0)
			movements.Sort((a, b) => a.Date.CompareTo(b.Date));
		return changed;

		static int Upsert(List<CashMovementEntity> target, FlexCashFlowDto flow, string key)
		{
			var entity = target.Find(x => x.Id == key);
			if (
				entity != null
				&& entity.Date == flow.Date
				&& entity.Kind == flow.Kind
				&& entity.Amount == flow.Amount
				&& entity.Currency == flow.Currency
				&& entity.AmountInCurrency == flow.AmountInCurrency
				&& entity.Description == flow.Description
			)
				return 0;
			if (entity == null)
			{
				entity = new CashMovementEntity { Id = key };
				target.Add(entity);
			}
			entity.Date = flow.Date;
			entity.Kind = flow.Kind;
			entity.Amount = flow.Amount;
			entity.Currency = flow.Currency;
			entity.AmountInCurrency = flow.AmountInCurrency;
			entity.Description = flow.Description;
			return 1;
		}
	}

	/// <summary>The movements on or after <paramref name="from"/> (all of them when null), oldest first.</summary>
	public static IReadOnlyList<CashMovementDto> FilterCashMovements(
		IEnumerable<CashMovementDto> movements,
		DateOnly? from
	) => movements.Where(x => from == null || x.Date >= from).OrderBy(x => x.Date).ToList();

	/// <summary>
	/// Which chart point each movement belongs to: the first of <paramref name="pointDates"/>
	/// (sorted) on or after its date — a deposit on a weekend shows on the next trading day, where
	/// net liquidation steps up (the same rule as the Flex merge's cash flows). Movements after
	/// the last point are left out. Keyed by point index, movements oldest first.
	/// </summary>
	public static IReadOnlyDictionary<int, IReadOnlyList<CashMovementDto>> AssignCashMovements(
		IReadOnlyList<DateOnly> pointDates,
		IEnumerable<CashMovementDto> movements
	)
	{
		var result = new Dictionary<int, List<CashMovementDto>>();
		var dates = pointDates.ToList();
		foreach (var movement in movements.OrderBy(x => x.Date))
		{
			var index = dates.BinarySearch(movement.Date);
			if (index < 0)
				index = ~index;
			if (index >= dates.Count)
				continue;
			// BinarySearch may land on any of equal dates; take the first
			while (index > 0 && dates[index - 1] == dates[index])
				index--;
			if (!result.TryGetValue(index, out var list))
				result[index] = list = [];
			list.Add(movement);
		}
		return result.ToDictionary(x => x.Key, x => (IReadOnlyList<CashMovementDto>)x.Value);
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
	/// Monday-first weeks starting with the week of the 1st, each day with its recorded P&amp;L and
	/// the <paramref name="cashMovements"/> made that day.
	/// </summary>
	public static IReadOnlyList<CalendarDayDto> BuildCalendarMonth(
		DateOnly month,
		IEnumerable<AccountDaySnapshotDto> days,
		DateOnly today,
		IEnumerable<CashMovementDto>? cashMovements = null
	)
	{
		var first = GetMonthStart(month);
		var start = GetWeekStart(first);
		var pnlByDate = days.Where(x => x.DailyPnl != null)
			.GroupBy(x => x.Date)
			.ToDictionary(x => x.Key, x => x.Last().DailyPnl);
		var movementsByDate = (cashMovements ?? [])
			.GroupBy(x => x.Date)
			.ToDictionary(x => x.Key, x => (IReadOnlyList<CashMovementDto>)x.ToList());

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
					CashMovements = movementsByDate.GetValueOrDefault(date) ?? [],
				};
			})
			.ToList();
	}

	/// <summary>The same base-currency amount, give or take rounding to the cent.</summary>
	private static bool SameAmount(double a, double b) => Math.Abs(a - b) < 0.005;
}
