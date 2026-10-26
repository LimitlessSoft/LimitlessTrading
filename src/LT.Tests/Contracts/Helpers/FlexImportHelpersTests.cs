using LT.Contracts.Constants;
using LT.Contracts.Entities;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class FlexImportHelpersTests
{
	private static readonly TimeZoneInfo NewYork = FlexHelpers.GetStatementTimeZone();

	// 10:00 in New York, Monday 26 Oct 2026
	private static readonly DateTimeOffset Now = new(2026, 10, 26, 14, 0, 0, TimeSpan.Zero);
	private static readonly DateOnly Today = new(2026, 10, 26);

	[Fact]
	public void A_cash_section_new_to_the_history_starts_it_over_from_this_statement()
	{
		var state = new FlexImportStateEntity
		{
			CoveredFrom = new DateOnly(2023, 1, 1),
			CoveredTo = new DateOnly(2026, 10, 18),
			BackfillComplete = true,
			LastActivitySuccess = Now.AddDays(-1),
			LastActivityAttempt = Now,
			HadCashTransactionsSection = false,
			HadTransfersSection = false,
		};

		var restarted = FlexImportHelpers.NoteCashSections(
			state,
			hasCashTransactions: true,
			hasTransfers: false,
			new DateOnly(2026, 10, 11),
			new DateOnly(2026, 10, 25)
		);

		Assert.True(restarted);
		Assert.Equal(
			(new DateOnly(2026, 10, 11), new DateOnly(2026, 10, 25), false),
			(state.CoveredFrom!.Value, state.CoveredTo!.Value, state.BackfillComplete)
		);
		Assert.Null(state.LastActivitySuccess);
		Assert.Null(state.LastActivityAttempt);
		Assert.Equal((true, false), (state.HadCashTransactionsSection, state.HadTransfersSection));
	}

	[Fact]
	public void Known_or_missing_cash_sections_and_a_first_import_dont_start_over()
	{
		var known = new FlexImportStateEntity
		{
			CoveredFrom = new DateOnly(2023, 1, 1),
			CoveredTo = new DateOnly(2026, 10, 18),
			BackfillComplete = true,
			HadCashTransactionsSection = true,
			HadTransfersSection = true,
		};
		var missing = new FlexImportStateEntity
		{
			CoveredFrom = new DateOnly(2023, 1, 1),
			CoveredTo = new DateOnly(2026, 10, 18),
			BackfillComplete = true,
		};
		var first = new FlexImportStateEntity();
		var (from, to) = (new DateOnly(2026, 10, 11), new DateOnly(2026, 10, 25));

		Assert.False(FlexImportHelpers.NoteCashSections(known, true, true, from, to));
		Assert.False(FlexImportHelpers.NoteCashSections(missing, false, false, from, to));
		Assert.False(FlexImportHelpers.NoteCashSections(first, true, true, from, to));
		Assert.True(known.BackfillComplete && missing.BackfillComplete);
		Assert.Equal(
			(false, false),
			(missing.HadCashTransactionsSection, missing.HadTransfersSection)
		);
		Assert.Equal((true, true), (first.HadCashTransactionsSection, first.HadTransfersSection));
	}

	[Fact]
	public void F5_rechecks_missing_cash_sections_at_most_every_few_minutes()
	{
		var state = new FlexImportStateEntity
		{
			LastActivitySuccess = Now,
			HadCashTransactionsSection = true,
			HadTransfersSection = false,
		};

		Assert.False(FlexImportHelpers.ShouldRecheckCashSections(state, Now.AddMinutes(1)));
		Assert.True(
			FlexImportHelpers.ShouldRecheckCashSections(
				state,
				Now + FlexConstants.MissingSectionRecheckInterval
			)
		);
		state.HadTransfersSection = true;
		Assert.False(FlexImportHelpers.ShouldRecheckCashSections(state, Now.AddDays(1)));
	}

	[Fact]
	public void The_first_range_is_the_year_up_to_yesterday()
	{
		var (from, to) = FlexImportHelpers.GetInitialRange(Today);

		Assert.Equal(new DateOnly(2026, 10, 25), to);
		Assert.Equal(FlexConstants.MaxDaysPerRequest, to.DayNumber - from.DayNumber + 1);
	}

	[Fact]
	public void Back_fill_asks_for_the_year_before_the_oldest_covered_day()
	{
		var (from, to) = FlexImportHelpers.GetBackfillRange(new DateOnly(2025, 10, 26));

		Assert.Equal(new DateOnly(2025, 10, 25), to);
		Assert.Equal(new DateOnly(2024, 10, 26), from);
	}

	[Fact]
	public void The_daily_range_overlaps_a_week_and_ends_yesterday()
	{
		var range = Assert.Single(
			FlexImportHelpers.GetForwardRanges(new DateOnly(2026, 10, 23), Today)
		);

		Assert.Equal((new DateOnly(2026, 10, 16), new DateOnly(2026, 10, 25)), range);
	}

	[Fact]
	public void A_long_gap_is_fetched_in_yearly_chunks()
	{
		var ranges = FlexImportHelpers.GetForwardRanges(new DateOnly(2024, 6, 1), Today);

		Assert.Equal(3, ranges.Count);
		Assert.All(
			ranges,
			x => Assert.True(x.To.DayNumber - x.From.DayNumber < FlexConstants.MaxDaysPerRequest)
		);
		Assert.Equal(new DateOnly(2026, 10, 25), ranges[^1].To);
		for (var i = 1; i < ranges.Count; i++)
			Assert.Equal(ranges[i - 1].To.AddDays(1), ranges[i].From);
	}

	[Fact]
	public void Activity_is_due_once_per_new_york_day()
	{
		var state = new FlexImportStateEntity();
		Assert.True(FlexImportHelpers.IsActivityDue(state, Now, NewYork));

		state.LastActivityAttempt = state.LastActivitySuccess = Now.AddHours(-1);
		Assert.False(FlexImportHelpers.IsActivityDue(state, Now, NewYork));

		// 01:00 the next day in New York
		Assert.True(FlexImportHelpers.IsActivityDue(state, Now.AddHours(15), NewYork));
	}

	[Fact]
	public void A_failed_activity_import_waits_the_retry_interval()
	{
		var state = new FlexImportStateEntity
		{
			LastActivitySuccess = Now.AddDays(-1),
			LastActivityAttempt = Now.AddMinutes(-10),
		};

		Assert.False(FlexImportHelpers.IsActivityDue(state, Now, NewYork));
		Assert.True(
			FlexImportHelpers.IsActivityDue(
				state,
				Now.AddMinutes(-10) + FlexConstants.ActivityRetryInterval,
				NewYork
			)
		);
	}

	[Fact]
	public void Trade_confirmations_are_due_every_interval()
	{
		var state = new FlexImportStateEntity { LastTradeConfirmAttempt = Now };

		Assert.False(FlexImportHelpers.IsTradeConfirmDue(state, Now.AddMinutes(1)));
		Assert.True(
			FlexImportHelpers.IsTradeConfirmDue(state, Now + FlexConstants.TradeConfirmInterval)
		);
	}

	[Fact]
	public void The_scheduler_sleeps_until_the_next_trade_confirmation_or_midnight()
	{
		var state = new FlexImportStateEntity
		{
			CoveredFrom = new DateOnly(2020, 1, 1),
			CoveredTo = new DateOnly(2026, 10, 25),
			BackfillComplete = true,
			LastActivityAttempt = Now,
			LastActivitySuccess = Now,
			LastTradeConfirmAttempt = Now,
		};

		Assert.Equal(
			FlexConstants.TradeConfirmInterval,
			FlexImportHelpers.GetDelayUntilDue(state, true, Now, NewYork)
		);
		// Midnight in New York is 04:00 UTC the next day
		Assert.Equal(
			TimeSpan.FromHours(14),
			FlexImportHelpers.GetDelayUntilDue(state, false, Now, NewYork)
		);
	}

	[Fact]
	public void Unfinished_back_fill_is_due_now()
	{
		var state = new FlexImportStateEntity
		{
			CoveredFrom = new DateOnly(2025, 10, 26),
			CoveredTo = new DateOnly(2026, 10, 25),
			LastActivityAttempt = Now,
			LastActivitySuccess = Now,
		};

		Assert.Equal(TimeSpan.Zero, FlexImportHelpers.GetDelayUntilDue(state, true, Now, NewYork));
	}

	[Fact]
	public void Coverage_only_grows()
	{
		var state = new FlexImportStateEntity();
		FlexImportHelpers.ExtendCoverage(state, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1));
		FlexImportHelpers.ExtendCoverage(state, new DateOnly(2026, 3, 1), new DateOnly(2026, 4, 1));

		Assert.Equal(
			(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1)),
			(state.CoveredFrom!.Value, state.CoveredTo!.Value)
		);
	}
}
