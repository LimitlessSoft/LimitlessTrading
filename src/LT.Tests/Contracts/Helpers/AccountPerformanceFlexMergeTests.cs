using LT.Contracts.Dtos.AccountPerformance;
using LT.Contracts.Dtos.Flex;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class AccountPerformanceFlexMergeTests
{
	private static readonly DateOnly Today = new(2026, 10, 26);
	private static readonly DateTimeOffset Now = new(2026, 10, 26, 14, 0, 0, TimeSpan.Zero);

	private static FlexNavDto Nav(int day, decimal total) =>
		new()
		{
			Account = "U0000001",
			Date = new DateOnly(2026, 10, day),
			Total = total,
			Currency = "USD",
		};

	private static FlexCashFlowDto Flow(int day, double amount) =>
		new()
		{
			Account = "U0000001",
			Date = new DateOnly(2026, 10, day),
			Amount = amount,
		};

	[Fact]
	public void A_flex_days_pnl_is_its_nav_change_without_the_deposit()
	{
		var days = new List<AccountDaySnapshotEntity>();

		var changed = AccountPerformanceHelpers.MergeFlexDays(
			days,
			[Nav(21, 10000), Nav(22, 11100), Nav(23, 11050)],
			[Flow(22, 1000)],
			Today,
			Now
		);

		Assert.Equal(3, changed);
		Assert.All(days, x => Assert.Equal(AccountDaySource.Flex, x.Source));
		Assert.Null(days[0].DailyPnl);
		Assert.Equal(100, days[1].DailyPnl);
		Assert.Equal(1000, days[1].CashFlow);
		Assert.Equal(-50, days[2].DailyPnl);
	}

	[Fact]
	public void A_deposit_never_counts_as_a_gain_in_the_return()
	{
		var days = new List<AccountDaySnapshotEntity>();
		AccountPerformanceHelpers.MergeFlexDays(
			days,
			[Nav(21, 10000), Nav(22, 20000)],
			[Flow(22, 10000)],
			Today,
			Now
		);
		var dto = new AccountDaySnapshotDto
		{
			NetLiquidation = days[1].NetLiquidation,
			DailyPnl = days[1].DailyPnl,
		};

		Assert.Equal(0, days[1].DailyPnl);
		Assert.Equal(0, AccountPerformanceHelpers.GetDailyReturn(dto));
	}

	[Fact]
	public void Today_stays_live()
	{
		var days = new List<AccountDaySnapshotEntity>
		{
			new()
			{
				Date = Today,
				NetLiquidation = 12345,
				DailyPnl = 45,
			},
		};

		AccountPerformanceHelpers.MergeFlexDays(days, [Nav(23, 12000), Nav(26, 1)], [], Today, Now);

		var today = days.Single(x => x.Date == Today);
		Assert.Equal(AccountDaySource.Recorded, today.Source);
		Assert.Equal(12345, today.NetLiquidation);
		Assert.Equal(45, today.DailyPnl);
	}

	[Fact]
	public void Flex_replaces_a_recorded_past_day_but_keeps_its_realized_and_unrealized()
	{
		var days = new List<AccountDaySnapshotEntity>
		{
			new()
			{
				Date = new DateOnly(2026, 10, 23),
				NetLiquidation = 11000,
				DailyPnl = 30,
				RealizedPnl = 10,
				UnrealizedPnl = 20,
			},
		};

		AccountPerformanceHelpers.MergeFlexDays(
			days,
			[Nav(22, 10000), Nav(23, 10040)],
			[],
			Today,
			Now
		);

		var day = days.Single(x => x.Date == new DateOnly(2026, 10, 23));
		Assert.Equal(
			(10040m, 40.0, 10.0, 20.0),
			(
				day.NetLiquidation!.Value,
				day.DailyPnl!.Value,
				day.RealizedPnl!.Value,
				day.UnrealizedPnl!.Value
			)
		);
	}

	[Fact]
	public void A_cash_flow_on_a_day_without_nav_goes_to_the_next_nav_day()
	{
		var days = new List<AccountDaySnapshotEntity>();

		AccountPerformanceHelpers.MergeFlexDays(
			days,
			[Nav(23, 10000), Nav(26 - 1, 10500)],
			[Flow(24, 500)],
			Today,
			Now
		);

		Assert.Equal(0, days[^1].DailyPnl);
	}

	[Fact]
	public void Merging_the_same_statement_again_changes_nothing()
	{
		var days = new List<AccountDaySnapshotEntity>();
		AccountPerformanceHelpers.MergeFlexDays(days, [Nav(21, 1), Nav(22, 2)], [], Today, Now);

		Assert.Equal(
			0,
			AccountPerformanceHelpers.MergeFlexDays(days, [Nav(21, 1), Nav(22, 2)], [], Today, Now)
		);
	}

	[Fact]
	public void An_older_chunk_fills_in_the_first_days_pnl()
	{
		var days = new List<AccountDaySnapshotEntity>();
		AccountPerformanceHelpers.MergeFlexDays(days, [Nav(22, 110)], [], Today, Now);
		Assert.Null(days[0].DailyPnl);

		AccountPerformanceHelpers.MergeFlexDays(days, [Nav(21, 100)], [], Today, Now);

		Assert.Equal(10, days.Single(x => x.Date.Day == 22).DailyPnl);
	}

	private static FlexCashFlowDto Movement(
		int day,
		double amount,
		CashMovementKind kind,
		string id = ""
	) =>
		new()
		{
			Account = "U0000001",
			Date = new DateOnly(2026, 10, day),
			Amount = amount,
			Kind = kind,
			TransactionId = id,
			Currency = "USD",
			AmountInCurrency = amount,
		};

	[Fact]
	public void Cash_movements_are_kept_by_key_sorted_and_merged_idempotently()
	{
		var movements = new List<CashMovementEntity>();
		var cash = new[]
		{
			Movement(22, -250, CashMovementKind.Withdrawal, "2"),
			Movement(20, 1000, CashMovementKind.Deposit), // no transactionID: keyed by its values
		};
		var transfers = new[] { Movement(21, 300, CashMovementKind.TransferIn, "3") };

		Assert.Equal(3, AccountPerformanceHelpers.MergeCashMovements(movements, cash, transfers));
		Assert.Equal(0, AccountPerformanceHelpers.MergeCashMovements(movements, cash, transfers));

		Assert.Equal(
			[CashMovementKind.Deposit, CashMovementKind.TransferIn, CashMovementKind.Withdrawal],
			movements.Select(x => x.Kind)
		);
		Assert.Equal("transfer:3", movements[1].Id);
		Assert.Equal("cash:2", movements[2].Id);

		// IB corrects the amount of a known transaction: updated in place
		cash[0].Amount = -260;
		Assert.Equal(1, AccountPerformanceHelpers.MergeCashMovements(movements, cash, []));
		Assert.Equal(-260, movements.Single(x => x.Id == "cash:2").Amount);
	}

	[Fact]
	public void A_transfer_listed_as_a_deposit_too_shows_once()
	{
		var movements = new List<CashMovementEntity>();
		AccountPerformanceHelpers.MergeCashMovements(
			movements,
			[Movement(22, 300, CashMovementKind.TransferIn, "7")],
			[]
		);

		// The same money in the Transfers section, imported later
		AccountPerformanceHelpers.MergeCashMovements(
			movements,
			[],
			[Movement(22, 300, CashMovementKind.TransferIn, "8")]
		);

		Assert.Equal("cash:7", Assert.Single(movements).Id);
	}

	[Fact]
	public void Movements_belong_to_the_first_chart_point_on_or_after_their_date()
	{
		DateOnly[] points = [new(2026, 10, 22), new(2026, 10, 23), new(2026, 10, 26)];
		var saturday = new CashMovementDto
		{
			Date = new DateOnly(2026, 10, 24),
			Kind = CashMovementKind.Deposit,
		};
		var thursday = new CashMovementDto
		{
			Date = new DateOnly(2026, 10, 22),
			Kind = CashMovementKind.Withdrawal,
		};
		var tomorrow = new CashMovementDto { Date = new DateOnly(2026, 10, 27) };

		var byPoint = AccountPerformanceHelpers.AssignCashMovements(
			points,
			[saturday, thursday, tomorrow]
		);

		Assert.Equal([0, 2], byPoint.Keys.Order());
		Assert.Same(thursday, Assert.Single(byPoint[0]));
		Assert.Same(saturday, Assert.Single(byPoint[2]));
	}

	[Fact]
	public void The_calendar_lists_each_days_movements_on_their_own_date()
	{
		var deposit = new CashMovementDto { Date = new DateOnly(2026, 10, 24), Amount = 500 };

		var cells = AccountPerformanceHelpers.BuildCalendarMonth(
			new DateOnly(2026, 10, 1),
			[],
			Today,
			[deposit]
		);

		Assert.Same(
			deposit,
			Assert.Single(cells.Single(x => x.Date == deposit.Date).CashMovements)
		);
		Assert.Equal(1, cells.Count(x => x.CashMovements.Count > 0));
		Assert.All(
			AccountPerformanceHelpers.BuildCalendarMonth(new DateOnly(2026, 10, 1), [], Today),
			x => Assert.Empty(x.CashMovements)
		);
	}
}
