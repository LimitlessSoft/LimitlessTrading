using LT.Contracts.Dtos.Executions;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class TradeMatchingHelpersTests
{
	private static readonly DateTimeOffset Start = new(2026, 10, 26, 14, 0, 0, TimeSpan.Zero);
	private int _execId;

	[Fact]
	public void A_buy_then_a_sell_is_one_long_trade_net_of_commission()
	{
		var trade = Assert.Single(
			TradeMatchingHelpers.Match([Fill("BOT", 100, 10, 0, 1), Fill("SLD", 100, 11, 5, 1)])
		);

		Assert.True(trade.IsClosed);
		Assert.Equal(TradeSide.Long, trade.Side);
		Assert.Equal(100, trade.Quantity);
		Assert.Equal(10, trade.EntryPrice, 6);
		Assert.Equal(11, trade.ExitPrice!.Value, 6);
		Assert.Equal(100, trade.GrossPnl, 6);
		Assert.Equal(2, trade.Commission, 6);
		Assert.Equal(98, trade.NetPnl, 6);
		Assert.Equal(TimeSpan.FromMinutes(5), trade.HoldTime);
		Assert.Equal(2, trade.FillCount);
	}

	[Fact]
	public void Scaling_in_and_out_stays_one_trade_with_fifo_pnl()
	{
		var trades = TradeMatchingHelpers.Match([
			Fill("BOT", 50, 10, 0),
			Fill("BOT", 50, 10.2, 1),
			Fill("SLD", 30, 11, 2),
			Fill("SLD", 70, 10.5, 3),
		]);

		var trade = Assert.Single(trades);
		Assert.True(trade.IsClosed);
		Assert.Equal(100, trade.Quantity);
		Assert.Equal(10.1, trade.EntryPrice, 6);
		Assert.Equal(10.65, trade.ExitPrice!.Value, 6);
		// 30 × 1.00 + 20 × 0.50 + 50 × 0.30
		Assert.Equal(55, trade.GrossPnl, 6);
		Assert.Equal(Start.AddMinutes(3), trade.CloseTime);
	}

	[Fact]
	public void A_sell_from_flat_opens_a_short()
	{
		var trade = Assert.Single(
			TradeMatchingHelpers.Match([Fill("SLD", 10, 50, 0), Fill("BOT", 10, 48, 1)])
		);

		Assert.Equal(TradeSide.Short, trade.Side);
		Assert.Equal(20, trade.GrossPnl, 6);
	}

	[Fact]
	public void A_fill_through_zero_closes_the_trade_and_opens_the_other_way()
	{
		var trades = TradeMatchingHelpers.Match([
			Fill("BOT", 100, 10, 0, 1),
			Fill("SLD", 150, 12, 1, 3),
			Fill("BOT", 50, 11, 2, 1),
		]);

		Assert.Equal(2, trades.Count);
		var (longTrade, shortTrade) = (trades[0], trades[1]);
		Assert.Equal(TradeSide.Long, longTrade.Side);
		Assert.Equal(200, longTrade.GrossPnl, 6);
		Assert.Equal(3, longTrade.Commission, 6); // 1 + 100/150 of 3
		Assert.Equal(TradeSide.Short, shortTrade.Side);
		Assert.Equal(50, shortTrade.Quantity);
		Assert.Equal(12, shortTrade.EntryPrice, 6);
		Assert.Equal(Start.AddMinutes(1), shortTrade.OpenTime);
		Assert.Equal(50, shortTrade.GrossPnl, 6);
		Assert.Equal(2, shortTrade.Commission, 6); // 50/150 of 3 + 1
	}

	[Fact]
	public void A_partly_closed_position_stays_open_with_realized_so_far()
	{
		var trade = Assert.Single(
			TradeMatchingHelpers.Match([Fill("BOT", 100, 10, 0), Fill("SLD", 40, 10.5, 1)])
		);

		Assert.False(trade.IsClosed);
		Assert.Null(trade.CloseTime);
		Assert.Equal(20, trade.GrossPnl, 6);
		Assert.Equal(10.5, trade.ExitPrice!.Value, 6);
	}

	[Fact]
	public void The_multiplier_scales_pnl()
	{
		var trade = Assert.Single(
			TradeMatchingHelpers.Match([
				Fill("BOT", 1, 2.0, 0, multiplier: 100, securityType: "OPT"),
				Fill("SLD", 1, 2.5, 1, multiplier: 100, securityType: "OPT"),
			])
		);

		Assert.Equal(50, trade.GrossPnl, 6);
	}

	[Fact]
	public void Contracts_and_accounts_are_matched_separately()
	{
		var trades = TradeMatchingHelpers.Match([
			Fill("BOT", 1, 2, 0, conId: 1),
			Fill("SLD", 1, 3, 1, conId: 2),
			Fill("BOT", 10, 100, 2, account: "DU1"),
			Fill("SLD", 10, 101, 3, account: "DU2"),
		]);

		Assert.Equal(4, trades.Count);
		Assert.All(trades, x => Assert.False(x.IsClosed));
	}

	[Fact]
	public void Fills_are_matched_in_time_order_whatever_the_input_order()
	{
		var buy = Fill("BOT", 10, 100, 0);
		var sell = Fill("SLD", 10, 101, 1);

		var trade = Assert.Single(TradeMatchingHelpers.Match([sell, buy]));

		Assert.Equal(TradeSide.Long, trade.Side);
		Assert.Equal(10, trade.GrossPnl, 6);
	}

	[Fact]
	public void Fills_of_an_unknown_side_or_without_quantity_are_ignored() =>
		Assert.Empty(TradeMatchingHelpers.Match([Fill("???", 10, 1, 0), Fill("BOT", 0, 1, 1)]));

	[Fact]
	public void Closed_trades_come_by_close_time_before_open_ones()
	{
		var trades = TradeMatchingHelpers.Match([
			Fill("BOT", 1, 10, 0, conId: 1),
			Fill("BOT", 1, 10, 1, conId: 2),
			Fill("SLD", 1, 11, 2, conId: 2),
			Fill("BOT", 1, 10, 3, conId: 3),
			Fill("SLD", 1, 11, 4, conId: 3),
		]);

		Assert.Equal([true, true, false], trades.Select(x => x.IsClosed));
		Assert.Equal(Start.AddMinutes(2), trades[0].CloseTime);
	}

	[Fact]
	public void Legs_pair_each_exit_with_the_fifo_entry_lots_it_closed()
	{
		var legs = TradeMatchingHelpers.MatchLegs([
			Fill("BOT", 50, 10, 0, commission: 1),
			Fill("BOT", 50, 10.2, 1),
			Fill("SLD", 30, 11, 2, commission: 3),
			Fill("SLD", 70, 9.9, 3),
		]);

		Assert.Equal(3, legs.Count);
		Assert.Equal(["e001", "e001", "e002"], legs.Select(x => x.EntryExecId));
		Assert.Equal(["e003", "e004", "e004"], legs.Select(x => x.ExitExecId));
		Assert.Equal([30m, 20m, 50m], legs.Select(x => x.Quantity));
		Assert.All(legs, x => Assert.Equal(TradeSide.Long, x.Side));

		// 30 × (11 - 10), commission 30/50 of 1 + all of 3
		Assert.Equal(Start, legs[0].EntryTime);
		Assert.Equal(Start.AddMinutes(2), legs[0].ExitTime);
		Assert.Equal(10, legs[0].EntryPrice, 6);
		Assert.Equal(11, legs[0].ExitPrice, 6);
		Assert.Equal(30, legs[0].GrossPnl, 6);
		Assert.Equal(3.6, legs[0].Commission, 6);
		// 20 × (9.9 - 10) and 50 × (9.9 - 10.2)
		Assert.Equal(-2, legs[1].GrossPnl, 6);
		Assert.Equal(-15, legs[2].NetPnl, 6);
	}

	[Fact]
	public void Legs_sum_to_the_trades_pnl_through_zero_and_skip_open_quantity()
	{
		var fills = new[]
		{
			Fill("BOT", 100, 10, 0, 1),
			Fill("SLD", 150, 12, 1, 3),
			Fill("BOT", 20, 11, 2, 1),
		};

		var legs = TradeMatchingHelpers.MatchLegs(fills);
		var trades = TradeMatchingHelpers.Match(fills);

		Assert.Equal(2, legs.Count);
		Assert.Equal(TradeSide.Long, legs[0].Side);
		Assert.Equal(100, legs[0].Quantity);
		Assert.Equal(TradeSide.Short, legs[1].Side);
		Assert.Equal(20, legs[1].Quantity);
		Assert.Equal(legs[1].EntryExecId, legs[0].ExitExecId);
		Assert.Equal(trades.Sum(x => x.GrossPnl), legs.Sum(x => x.GrossPnl), 6);
	}

	[Fact]
	public void An_open_position_has_no_legs() =>
		Assert.Empty(
			TradeMatchingHelpers.MatchLegs([Fill("BOT", 10, 1, 0), Fill("BOT", 10, 2, 1)])
		);

	[Theory]
	[InlineData("BOT", 1)]
	[InlineData("buy", 1)]
	[InlineData("SLD", -1)]
	[InlineData("SELL", -1)]
	[InlineData("", 0)]
	public void Side_gives_the_direction(string side, int expected) =>
		Assert.Equal(expected, TradeMatchingHelpers.GetDirection(side));

	private ExecutionDto Fill(
		string side,
		decimal shares,
		double price,
		int minute,
		double? commission = null,
		int conId = 265598,
		string account = "DU1",
		double? multiplier = null,
		string securityType = "STK"
	) =>
		new()
		{
			ExecId = $"e{++_execId:000}",
			Time = Start.AddMinutes(minute),
			Account = account,
			ConId = conId,
			Symbol = "AAPL",
			SecurityType = securityType,
			Side = side,
			Shares = shares,
			Price = price,
			Commission = commission,
			Multiplier = multiplier,
		};
}
