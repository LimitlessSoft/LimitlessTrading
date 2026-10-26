using LT.Contracts.Constants;
using LT.Contracts.Dtos.Account;
using LT.Contracts.Dtos.AccountPerformance;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class AccountPerformanceHelpersTests
{
	private static readonly DateOnly Monday = new(2026, 10, 26);

	[Fact]
	public void Trading_date_is_the_date_in_New_York()
	{
		var zone = AccountPerformanceHelpers.GetTradingDayTimeZone();

		// 02:00 UTC on the 27th is 22:00 on the 26th in New York (EDT)
		var date = AccountPerformanceHelpers.GetTradingDate(
			new DateTimeOffset(2026, 10, 27, 2, 0, 0, TimeSpan.Zero),
			zone
		);

		Assert.Equal(new DateOnly(2026, 10, 26), date);
	}

	[Fact]
	public void Daily_return_is_pnl_over_the_equity_the_day_started_with()
	{
		var day = Day(Monday, 10_100m, 100);

		Assert.Equal(0.01, AccountPerformanceHelpers.GetDailyReturn(day)!.Value, 10);
	}

	[Fact]
	public void Daily_return_needs_both_values()
	{
		Assert.Null(AccountPerformanceHelpers.GetDailyReturn(Day(Monday, null, 100)));
		Assert.Null(AccountPerformanceHelpers.GetDailyReturn(Day(Monday, 10_000m, null)));
	}

	[Fact]
	public void Period_return_chains_daily_returns_and_sums_pnl()
	{
		var days = new[]
		{
			Day(Monday, 10_100m, 100), // +1 %
			Day(Monday.AddDays(1), 9_999m, -101), // -1 %
		};

		var period = AccountPerformanceHelpers.GetPeriodReturn(days, Monday, Monday.AddDays(1));

		Assert.Equal(-1, period.Pnl);
		Assert.Equal((1.01 * 0.99 - 1) * 100, period.Percent!.Value, 10);
	}

	[Fact]
	public void A_deposit_is_not_a_gain()
	{
		var days = new[]
		{
			Day(Monday, 10_100m, 100),
			// 10,000 deposited, nothing earned: NetLiq jumps, daily P&L stays 0
			Day(Monday.AddDays(1), 20_100m, 0),
		};

		var period = AccountPerformanceHelpers.GetPeriodReturn(days, Monday, Monday.AddDays(1));

		Assert.Equal(100, period.Pnl);
		Assert.Equal(1, period.Percent!.Value, 10);
	}

	[Fact]
	public void Period_return_only_counts_days_in_the_span()
	{
		var days = new[] { Day(Monday.AddDays(-1), 10_100m, 100), Day(Monday, 10_150m, 50) };

		var period = AccountPerformanceHelpers.GetPeriodReturn(days, Monday, Monday);

		Assert.Equal(50, period.Pnl);
	}

	[Fact]
	public void Period_return_without_data_is_empty()
	{
		var period = AccountPerformanceHelpers.GetPeriodReturn([], Monday, Monday);

		Assert.Null(period.Pnl);
		Assert.Null(period.Percent);
	}

	[Fact]
	public void Max_drawdown_is_the_largest_fall_from_a_peak()
	{
		var days = new[]
		{
			Day(Monday, 10_100m, 100), // peak 100, index 1.01
			Day(Monday.AddDays(1), 9_800m, -300), // 300 below the peak
			Day(Monday.AddDays(2), 9_850m, 50),
		};

		var drawdown = AccountPerformanceHelpers.GetMaxDrawdown(days);

		Assert.Equal(-300, drawdown.Money);
		Assert.Equal((-300.0 / 10_100) * 100, drawdown.Percent!.Value, 10);
	}

	[Fact]
	public void A_losing_first_day_is_a_drawdown()
	{
		var drawdown = AccountPerformanceHelpers.GetMaxDrawdown([Day(Monday, 9_900m, -100)]);

		Assert.Equal(-100, drawdown.Money);
		Assert.Equal(-1, drawdown.Percent!.Value, 10);
	}

	[Fact]
	public void Only_gains_have_no_drawdown()
	{
		var drawdown = AccountPerformanceHelpers.GetMaxDrawdown([
			Day(Monday, 10_100m, 100),
			Day(Monday.AddDays(1), 10_200m, 100),
		]);

		Assert.Equal(0, drawdown.Money);
		Assert.Equal(0, drawdown.Percent);
	}

	[Theory]
	[InlineData("2026-10-26", "2026-10-26")]
	[InlineData("2026-10-28", "2026-10-26")]
	[InlineData("2026-11-01", "2026-10-26")]
	public void Weeks_start_on_Monday(string date, string expected) =>
		Assert.Equal(
			DateOnly.Parse(expected),
			AccountPerformanceHelpers.GetWeekStart(DateOnly.Parse(date))
		);

	[Fact]
	public void Range_starts()
	{
		Assert.Equal(
			new DateOnly(2026, 10, 20),
			AccountPerformanceHelpers.GetRangeStart(PerformanceRange.Week, Monday)
		);
		Assert.Equal(
			new DateOnly(2026, 9, 27),
			AccountPerformanceHelpers.GetRangeStart(PerformanceRange.Month, Monday)
		);
		Assert.Equal(
			new DateOnly(2026, 1, 1),
			AccountPerformanceHelpers.GetRangeStart(PerformanceRange.YearToDate, Monday)
		);
		Assert.Null(AccountPerformanceHelpers.GetRangeStart(PerformanceRange.All, Monday));
	}

	[Fact]
	public void Filter_range_keeps_days_from_the_start_oldest_first()
	{
		var days = new[]
		{
			Day(Monday, 1m, 1),
			Day(Monday.AddDays(-10), 1m, 1),
			Day(Monday.AddDays(-2), 1m, 1),
		};

		var filtered = AccountPerformanceHelpers.FilterRange(days, Monday.AddDays(-6));

		Assert.Equal([Monday.AddDays(-2), Monday], filtered.Select(x => x.Date));
	}

	[Fact]
	public void Calendar_is_six_Monday_first_weeks_with_the_recorded_pnl()
	{
		var october = new DateOnly(2026, 10, 1); // a Thursday
		var days = new[] { Day(new DateOnly(2026, 10, 15), 1m, 42), Day(Monday, 1m, null) };

		var cells = AccountPerformanceHelpers.BuildCalendarMonth(october, days, Monday);

		Assert.Equal(AccountPerformanceConstants.CalendarWeeks * 7, cells.Count);
		Assert.Equal(new DateOnly(2026, 9, 28), cells[0].Date);
		Assert.False(cells[0].IsInMonth);
		Assert.True(cells[3].IsInMonth);
		Assert.Equal(42, cells.Single(x => x.Date == new DateOnly(2026, 10, 15)).Pnl);
		Assert.Null(cells.Single(x => x.Date == Monday).Pnl);
		Assert.Equal([Monday], cells.Where(x => x.IsToday).Select(x => x.Date));
	}

	[Fact]
	public void A_new_day_is_inserted_in_order_and_carries_net_liquidation()
	{
		var now = DateTimeOffset.UtcNow;
		var days = new List<AccountDaySnapshotEntity>
		{
			new() { Date = Monday.AddDays(-2) },
			new() { Date = Monday.AddDays(2) },
		};
		var carry = new AccountSummaryDto { NetLiquidation = 5_000m, Currency = "EUR" };

		var day = AccountPerformanceHelpers.GetOrAddDay(days, Monday, now, carry);

		Assert.Equal([Monday.AddDays(-2), Monday, Monday.AddDays(2)], days.Select(x => x.Date));
		Assert.Equal((5_000m, "EUR", now), (day.NetLiquidation, day.Currency, day.FirstUpdate));
		Assert.Same(
			day,
			AccountPerformanceHelpers.GetOrAddDay(days, Monday, now.AddHours(1), null)
		);
	}

	[Fact]
	public void Missing_pnl_values_keep_the_previous_ones()
	{
		var day = new AccountDaySnapshotEntity
		{
			DailyPnl = 10,
			RealizedPnl = 5,
			UnrealizedPnl = 3,
		};

		AccountPerformanceHelpers.ApplyPnl(day, new AccountPnlDto { DailyPnl = 12 });

		Assert.Equal(
			(12.0, 5.0, 3.0),
			(day.DailyPnl!.Value, day.RealizedPnl!.Value, day.UnrealizedPnl!.Value)
		);
	}

	private static AccountDaySnapshotDto Day(DateOnly date, decimal? netLiquidation, double? pnl) =>
		new()
		{
			Date = date,
			NetLiquidation = netLiquidation,
			DailyPnl = pnl,
		};
}
