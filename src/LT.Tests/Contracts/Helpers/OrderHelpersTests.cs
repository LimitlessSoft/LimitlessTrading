using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Dtos.Positions;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.Orders;

namespace LT.Tests.Contracts.Helpers;

public class OrderHelpersTests
{
	[Fact]
	public void Nothing_can_be_cancelled_while_disconnected() =>
		Assert.False(
			OrderHelpers.CanCancel(new OpenOrderDto { ClientId = 101, Status = "Submitted" }, null)
		);

	[Theory]
	[InlineData("PreSubmitted", true)]
	[InlineData("Submitted", true)]
	[InlineData("PendingCancel", false)]
	[InlineData("Filled", false)]
	public void An_own_order_can_be_cancelled_until_a_cancel_or_fill(
		string status,
		bool expected
	) =>
		Assert.Equal(
			expected,
			OrderHelpers.CanCancel(new OpenOrderDto { ClientId = 101, Status = status }, 101)
		);

	[Theory]
	[InlineData(1, 101)]
	[InlineData(32700, 32)]
	public void Order_client_id_is_offset_within_ibs_range(int clientId, int expected) =>
		Assert.Equal(expected, OrderHelpers.GetOrderClientId(clientId));

	[Fact]
	public void Crypto_market_orders_are_ioc_only() =>
		Assert.Equal([TimeInForce.Ioc], OrderHelpers.GetTimeInForces("CRYPTO", OrderType.Market));

	[Fact]
	public void Stocks_default_to_day() =>
		Assert.Equal(TimeInForce.Day, OrderHelpers.GetTimeInForces("STK", OrderType.Limit)[0]);

	[Theory]
	[InlineData("10", 10)]
	[InlineData(" 0.0002 ", 0.0002)]
	[InlineData("1,000.5", 1000.5)]
	public void Numbers_parse_invariantly(string text, double expected) =>
		Assert.Equal((decimal)expected, OrderHelpers.ParseNumber(text));

	[Theory]
	[InlineData("")]
	[InlineData("abc")]
	public void Non_numbers_parse_as_null(string text) =>
		Assert.Null(OrderHelpers.ParseNumber(text));

	[Fact]
	public void Describe_spells_out_a_cash_order() =>
		Assert.Equal(
			"Buy $10.00 of BTC.USD (PAXOS) at market, IOC, account U0000001",
			OrderHelpers.Describe(
				new PlaceOrderRequest
				{
					Contract = new ContractDto
					{
						ConId = 1,
						Symbol = "BTC",
						LocalSymbol = "BTC.USD",
						SecurityType = "CRYPTO",
						Exchange = "PAXOS",
					},
					Account = "U0000001",
					Action = OrderAction.Buy,
					OrderType = OrderType.Market,
					CashAmount = 10,
					TimeInForce = TimeInForce.Ioc,
				}
			)
		);

	[Fact]
	public void Reference_price_is_the_ask_for_a_buy_and_the_bid_for_a_sell()
	{
		Assert.Equal(101, OrderHelpers.GetReferencePrice(OrderAction.Buy, 99, 101, 100));
		Assert.Equal(99, OrderHelpers.GetReferencePrice(OrderAction.Sell, 99, 101, 100));
		Assert.Equal(100, OrderHelpers.GetReferencePrice(OrderAction.Buy, null, null, 100));
		Assert.Null(OrderHelpers.GetReferencePrice(OrderAction.Buy, null, -1, null));
	}

	[Fact]
	public void Estimate_converts_an_amount_to_units_and_a_quantity_to_money()
	{
		Assert.Equal("≈ 0.000125 units", OrderHelpers.Estimate(null, 10, 80000));
		Assert.Equal("≈ $16.00", OrderHelpers.Estimate(0.0002m, null, 80000));
		Assert.Null(OrderHelpers.Estimate(1, null, null));
	}

	private static PositionDto Position(decimal quantity, string securityType = "STK") =>
		new()
		{
			Account = "U1234567",
			ContractId = 265598,
			Symbol = "AAPL",
			Quantity = quantity,
			Contract = new ContractDto
			{
				ConId = 265598,
				Symbol = "AAPL",
				SecurityType = securityType,
			},
		};

	[Fact]
	public void Closing_a_long_position_sells_all_of_it()
	{
		var draft = OrderHelpers.GetClosingDraft(Position(100));

		Assert.NotNull(draft);
		Assert.Equal(OrderAction.Sell, draft.Action);
		Assert.Equal(100m, draft.Quantity);
		Assert.Equal("U1234567", draft.Account);
		Assert.Equal(265598, draft.Contract.ConId);
	}

	[Fact]
	public void Closing_a_short_position_buys_it_back()
	{
		var draft = OrderHelpers.GetClosingDraft(Position(-25));

		Assert.NotNull(draft);
		Assert.Equal(OrderAction.Buy, draft.Action);
		Assert.Equal(25m, draft.Quantity);
		Assert.Contains("short", draft.Note);
	}

	[Fact]
	public void A_fractional_position_closes_in_full()
	{
		var draft = OrderHelpers.GetClosingDraft(Position(0.00012345m));

		Assert.NotNull(draft);
		Assert.Equal(0.00012345m, draft.Quantity);
		Assert.Equal(
			0.00012345m,
			OrderHelpers.ParseNumber(OrderHelpers.FormatInput(draft.Quantity))
		);
	}

	[Fact]
	public void A_flat_position_has_nothing_to_close() =>
		Assert.Null(OrderHelpers.GetClosingDraft(Position(0)));

	[Theory]
	[InlineData("1500", 1500)]
	[InlineData("0.5", 0.5)]
	public void Input_text_reads_back_as_the_same_number(string expected, double value)
	{
		var text = OrderHelpers.FormatInput((decimal)value);

		Assert.Equal(expected, text);
		Assert.Equal((decimal)value, OrderHelpers.ParseNumber(text));
	}

	[Theory]
	[InlineData(100, OrderAction.Sell, 100)]
	[InlineData(-2.5, OrderAction.Buy, 2.5)]
	public void Quick_exit_is_a_market_order_for_the_whole_position(
		double quantity,
		OrderAction action,
		double expected
	)
	{
		var request = OrderHelpers.GetQuickExitRequest(Position((decimal)quantity));

		Assert.NotNull(request);
		Assert.Equal(
			(
				action,
				OrderType.Market,
				(decimal?)expected,
				(decimal?)null,
				TimeInForce.Day,
				"U1234567",
				265598
			),
			(
				request.Action,
				request.OrderType,
				request.Quantity,
				request.CashAmount,
				request.TimeInForce,
				request.Account,
				request.Contract!.ConId
			)
		);
	}

	[Fact]
	public void Quick_exit_of_crypto_is_ioc() =>
		Assert.Equal(
			TimeInForce.Ioc,
			OrderHelpers.GetQuickExitRequest(Position(0.5m, "CRYPTO"))!.TimeInForce
		);

	[Fact]
	public void A_flat_position_has_no_quick_exit() =>
		Assert.Null(OrderHelpers.GetQuickExitRequest(Position(0)));

	private static OpenOrderDto Working(
		int clientId = 101,
		string type = "LMT",
		double? limit = 1.60,
		string status = "Submitted"
	) =>
		new()
		{
			OrderId = 44,
			ClientId = clientId,
			Account = "U0000001",
			Symbol = "SPY 20261029 767 P",
			Action = "SELL",
			Quantity = 1,
			OrderType = type,
			LimitPrice = limit,
			TimeInForce = "DAY",
			Status = status,
		};

	[Fact]
	public void This_apps_working_limit_order_can_be_changed() =>
		Assert.Null(OrderHelpers.GetModifyBlock(Working(), 101));

	[Theory]
	[InlineData(0)] // TWS
	[InlineData(1)] // the app's main connection never places orders
	[InlineData(7)] // another API client
	public void Only_the_order_connections_orders_can_be_changed(int clientId) =>
		Assert.Equal(
			OrderValidationCodes.OVC_014,
			OrderHelpers.GetModifyBlock(Working(clientId), 101)
		);

	[Fact]
	public void Nothing_can_be_changed_while_disconnected() =>
		Assert.Equal(OrderValidationCodes.OVC_014, OrderHelpers.GetModifyBlock(Working(), null));

	[Theory]
	[InlineData("MKT", null)]
	[InlineData("STP", null)]
	[InlineData("STP LMT", 1.5)]
	[InlineData("LMT", null)]
	public void Only_a_limit_orders_price_can_be_changed(string type, double? limit) =>
		Assert.Equal(
			OrderValidationCodes.OVC_015,
			OrderHelpers.GetModifyBlock(Working(type: type, limit: limit), 101)
		);

	[Theory]
	[InlineData("Filled")]
	[InlineData("Cancelled")]
	[InlineData("ApiCancelled")]
	[InlineData("Inactive")]
	[InlineData("PendingCancel")]
	public void An_order_that_is_done_or_being_cancelled_cant_be_changed(string status) =>
		Assert.Equal(
			OrderValidationCodes.OVC_016,
			OrderHelpers.GetModifyBlock(Working(status: status), 101)
		);

	[Fact]
	public void A_change_is_described_with_the_old_and_new_price() =>
		Assert.Equal(
			"SELL 1 SPY 20261029 767 P, limit 1.60 → 1.55, DAY, account U0000001 (order 44)",
			OrderHelpers.DescribeModification(Working(), 1.55)
		);
}
