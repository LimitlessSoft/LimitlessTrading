using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class ChartOrderHelpersTests
{
	private const int SpyPut = 800000001;

	private static OpenOrderDto Order(
		int orderId,
		string type = "LMT",
		double? limit = 1.60,
		double? aux = null,
		string status = "Submitted",
		string action = "SELL",
		string account = "U0000001",
		int conId = SpyPut
	) =>
		new()
		{
			OrderId = orderId,
			ClientId = 101,
			PermId = 900000000 + orderId,
			Account = account,
			Symbol = "SPY 20261029 767 P",
			Action = action,
			Quantity = 2,
			Filled = 0.5m,
			OrderType = type,
			LimitPrice = limit,
			AuxPrice = aux,
			TimeInForce = "DAY",
			Status = status,
			Contract = new ContractDto { ConId = conId },
		};

	[Theory]
	[InlineData(1.5549, 0.01, 1.55)]
	[InlineData(0.30000000000000004, 0.1, 0.3)]
	[InlineData(4512.37, 0.25, 4512.25)]
	[InlineData(4512.38, 0.25, 4512.5)]
	[InlineData(0.0012345, 0.0001, 0.0012)]
	[InlineData(0.001, 0.05, 0.05)]
	[InlineData(-3, 0.01, 0.01)]
	public void A_price_snaps_to_the_nearest_tick_and_never_below_one(
		double price,
		double tick,
		double snapped
	) => Assert.Equal(snapped, ChartOrderHelpers.SnapToTick(price, tick));

	[Theory]
	[InlineData(0)]
	[InlineData(-0.01)]
	[InlineData(double.NaN)]
	public void Without_a_tick_the_price_stays(double tick) =>
		Assert.Equal(1.234, ChartOrderHelpers.SnapToTick(1.234, tick));

	[Theory]
	[InlineData(0.01, 2)]
	[InlineData(0.0001, 4)]
	[InlineData(0.25, 2)]
	[InlineData(0.005, 3)]
	[InlineData(5, 0)]
	public void A_tick_needs_its_decimals(double tick, int decimals) =>
		Assert.Equal(decimals, ChartOrderHelpers.GetDecimals(tick));

	[Fact]
	public void Only_the_charted_contracts_working_orders_of_the_account_draw_a_line()
	{
		OpenOrderDto[] orders =
		[
			Order(44),
			Order(45, conId: 756733),
			Order(46, account: "U0000002"),
			Order(47, status: "Filled"),
			Order(48, status: "Cancelled"),
			Order(49, status: "PendingCancel"),
		];

		var lines = ChartOrderHelpers.GetOrderLines(orders, SpyPut, "U0000001", _ => null);

		Assert.Equal([44, 49], lines.Select(x => x.OrderId));
		Assert.Equal(
			[44, 46, 49],
			ChartOrderHelpers.GetOrderLines(orders, SpyPut, null, _ => null).Select(x => x.OrderId)
		);
	}

	[Fact]
	public void A_line_carries_the_side_what_is_left_and_the_limit_price()
	{
		var line = Assert.Single(
			ChartOrderHelpers.GetOrderLines([Order(44)], SpyPut, null, _ => null)
		);

		Assert.Equal("SELL", line.Action);
		Assert.False(line.IsBuy);
		Assert.Equal(1.5m, line.Quantity);
		Assert.Equal("LMT", line.OrderType);
		Assert.Equal(1.60, line.Price);
		Assert.False(line.IsStopPrice);
		Assert.True(line.CanMove);
		Assert.False(line.IsPending);
	}

	[Fact]
	public void A_stop_limit_order_draws_its_limit_and_its_stop_and_neither_moves()
	{
		var lines = ChartOrderHelpers.GetOrderLines(
			[Order(44, "STP LMT", limit: 1.50, aux: 1.55, action: "BUY")],
			SpyPut,
			null,
			_ => "limit orders only"
		);

		Assert.Equal([(1.50, false), (1.55, true)], lines.Select(x => (x.Price, x.IsStopPrice)));
		Assert.All(lines, x => Assert.True(x.IsBuy));
		Assert.All(lines, x => Assert.False(x.CanMove));
	}

	[Fact]
	public void A_stop_price_line_never_moves_even_when_the_order_could()
	{
		var stop = Assert.Single(
			ChartOrderHelpers.GetOrderLines(
				[Order(44, "STP", limit: null, aux: 1.2)],
				SpyPut,
				null,
				_ => null
			)
		);

		Assert.True(stop.IsStopPrice);
		Assert.False(stop.CanMove);
	}

	[Fact]
	public void The_reason_an_order_cant_move_goes_on_its_line()
	{
		var line = Assert.Single(
			ChartOrderHelpers.GetOrderLines([Order(44)], SpyPut, null, _ => "Placed in TWS")
		);

		Assert.Equal("Placed in TWS", line.MoveBlockedReason);
		Assert.False(line.CanMove);
	}

	[Fact]
	public void A_pending_price_moves_only_its_line_and_keeps_the_old_one()
	{
		var orders = new[] { Order(44), Order(45, limit: 2.10) };
		var id = ChartOrderHelpers.GetLineId(orders[0], false);

		var lines = ChartOrderHelpers.GetOrderLines(orders, SpyPut, null, _ => null, (id, 1.55));

		Assert.Equal(1.55, lines[0].Price);
		Assert.Equal(1.60, lines[0].PendingFromPrice);
		Assert.True(lines[0].IsPending);
		Assert.Equal(2.10, lines[1].Price);
		Assert.False(lines[1].IsPending);
	}
}
