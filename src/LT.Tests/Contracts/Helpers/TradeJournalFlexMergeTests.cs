using LT.Contracts.Entities;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class TradeJournalFlexMergeTests
{
	private static FillEntity Fill(string execId, string tradeId = "", double? commission = null) =>
		new()
		{
			ExecId = execId,
			TradeId = tradeId,
			Account = "U0000001",
			ConId = 1001,
			Side = "BUY",
			Shares = 100,
			Price = 10,
			Commission = commission,
		};

	[Fact]
	public void A_flex_fill_with_the_socket_exec_id_merges_into_it()
	{
		var fills = new List<FillEntity> { Fill("0000a.01") };

		Assert.True(TradeJournalHelpers.MergeFill(fills, Fill("0000a.01", "5001", 1.25)));

		var fill = Assert.Single(fills);
		Assert.Equal(("5001", 1.25), (fill.TradeId, fill.Commission!.Value));
	}

	[Fact]
	public void A_trade_confirmation_and_an_activity_row_match_by_trade_id()
	{
		var fills = new List<FillEntity> { Fill("flex:5001", "5001") };

		Assert.True(TradeJournalHelpers.MergeFill(fills, Fill("0000a.01", "5001")));

		// the real exec id replaces the flex key, so a later socket fill matches too
		Assert.Equal("0000a.01", Assert.Single(fills).ExecId);
		Assert.False(TradeJournalHelpers.MergeFill(fills, Fill("0000a.01")));
	}

	[Fact]
	public void Cancelled_trades_are_removed()
	{
		var fills = new List<FillEntity> { Fill("a", "1"), Fill("b", "2"), Fill("c") };

		Assert.Equal(1, TradeJournalHelpers.RemoveTrades(fills, ["2"]));
		Assert.Equal(["a", "c"], fills.Select(x => x.ExecId));
	}
}
