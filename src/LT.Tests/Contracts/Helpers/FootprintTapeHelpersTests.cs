using LT.Contracts.Dtos.Charts;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

/// <summary>The footprint trade cache's stretches, gaps and slices.</summary>
public class FootprintTapeHelpersTests
{
	private static readonly DateTimeOffset T0 = new(2026, 10, 28, 13, 30, 0, TimeSpan.Zero);

	private static DateTimeOffset S(int second) => T0.AddSeconds(second);

	[Fact]
	public void The_gaps_are_what_the_covered_stretches_leave_out()
	{
		List<(DateTimeOffset, DateTimeOffset)> covered = [(S(10), S(20)), (S(30), S(40))];

		Assert.Equal(
			[(S(0), S(10)), (S(20), S(30)), (S(40), S(50))],
			FootprintTapeHelpers.GetGaps(covered, S(0), S(50))
		);
		Assert.Equal([(S(20), S(25))], FootprintTapeHelpers.GetGaps(covered, S(15), S(25)));
		Assert.Empty(FootprintTapeHelpers.GetGaps(covered, S(12), S(18)));
		Assert.Equal([(S(0), S(5))], FootprintTapeHelpers.GetGaps([], S(0), S(5)));
	}

	[Fact]
	public void Adding_a_stretch_merges_what_it_touches()
	{
		List<(DateTimeOffset From, DateTimeOffset To)> covered = [(S(10), S(20)), (S(30), S(40))];

		FootprintTapeHelpers.AddCovered(covered, S(20), S(30));
		FootprintTapeHelpers.AddCovered(covered, S(50), S(60));
		FootprintTapeHelpers.AddCovered(covered, S(0), S(5));

		Assert.Equal([(S(0), S(5)), (S(10), S(40)), (S(50), S(60))], covered);
	}

	[Fact]
	public void A_slice_ends_between_two_seconds()
	{
		var range = new FootprintTapeRangeDto
		{
			From = S(0),
			To = S(10),
			Trades = [.. new[] { 1, 2, 2, 2, 3, 5 }.Select(Trade)],
		};

		var slices = FootprintTapeHelpers.Slice(range, 2).ToList();

		// 1, 2 | the rest of second 2 stays with it | 3, 5
		Assert.Equal([(S(0), S(3)), (S(3), S(10))], slices.Select(x => (x.From, x.To)));
		Assert.Equal([4, 2], slices.Select(x => x.Trades.Count));
	}

	[Fact]
	public void Live_trades_keep_their_side_only_when_the_quote_rule_told_it()
	{
		var byQuote = FootprintTapeHelpers.ToTapeTrade(
			new FootprintTradeDto
			{
				Time = S(1),
				Price = 10,
				Size = 5,
				Side = FootprintSide.Ask,
			}
		);
		var byTickRule = FootprintTapeHelpers.ToTapeTrade(
			new FootprintTradeDto
			{
				Time = S(1),
				Price = 10,
				Size = 5,
				Side = FootprintSide.Bid,
				ByTickRule = true,
			}
		);

		Assert.Equal(FootprintSide.Ask, byQuote.QuoteSide);
		Assert.Equal(FootprintSide.Unknown, byTickRule.QuoteSide);
	}

	private static FootprintTapeTradeDto Trade(int second) =>
		new(S(second).ToUnixTimeSeconds(), 10, 1, FootprintSide.Unknown);
}
