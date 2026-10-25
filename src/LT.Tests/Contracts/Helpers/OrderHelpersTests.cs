using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.Orders;

namespace LT.Tests.Contracts.Helpers;

public class OrderHelpersTests
{
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
}
