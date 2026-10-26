using LT.Contracts.Entities;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class TradeJournalHelpersTests
{
	private static readonly TimeZoneInfo NewYork = TradeJournalHelpers.GetTradeDayTimeZone();

	[Fact]
	public void A_fill_is_added_once_per_exec_id()
	{
		var fills = new List<FillEntity>();

		Assert.True(TradeJournalHelpers.MergeFill(fills, Fill("e1", null)));
		Assert.False(TradeJournalHelpers.MergeFill(fills, Fill("e1", null)));

		Assert.Single(fills);
	}

	[Fact]
	public void A_commission_arriving_later_is_merged_in()
	{
		var fills = new List<FillEntity> { Fill("e1", null) };

		Assert.True(TradeJournalHelpers.MergeFill(fills, Fill("e1", 1.25)));
		Assert.False(TradeJournalHelpers.MergeFill(fills, Fill("e1", null)));

		Assert.Equal(1.25, Assert.Single(fills).Commission);
		Assert.Equal("USD", fills[0].CommissionCurrency);
	}

	[Fact]
	public void A_fill_without_exec_id_is_skipped()
	{
		var fills = new List<FillEntity>();

		Assert.False(TradeJournalHelpers.MergeFill(fills, Fill("", null)));
		Assert.Empty(fills);
	}

	[Fact]
	public void After_hours_past_midnight_utc_stays_on_the_new_york_day() =>
		Assert.Equal(
			new DateOnly(2026, 10, 26),
			TradeJournalHelpers.GetTradeDay(
				new DateTimeOffset(2026, 10, 27, 1, 30, 0, TimeSpan.Zero),
				NewYork
			)
		);

	[Fact]
	public void Previous_and_next_skip_to_recorded_days()
	{
		DateOnly[] days = [new(2026, 10, 20), new(2026, 10, 22), new(2026, 10, 26)];
		var day = new DateOnly(2026, 10, 22);

		Assert.Equal(new DateOnly(2026, 10, 20), TradeJournalHelpers.GetPreviousDay(days, day));
		Assert.Equal(new DateOnly(2026, 10, 26), TradeJournalHelpers.GetNextDay(days, day));
		Assert.Null(TradeJournalHelpers.GetPreviousDay(days, days[0]));
		Assert.Null(TradeJournalHelpers.GetNextDay(days, days[^1]));
	}

	private static FillEntity Fill(string execId, double? commission) =>
		new()
		{
			ExecId = execId,
			Commission = commission,
			CommissionCurrency = commission != null ? "USD" : string.Empty,
		};
}
