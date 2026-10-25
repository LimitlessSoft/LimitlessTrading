using LT.Contracts.Dtos.Orders;
using LT.Contracts.Enums;
using LT.Ibkr.DtoMappings.Orders;
using Order = IBKR.Net.Order;

namespace LT.Tests.Ibkr;

public class IbkrOrderMapperTests
{
	private readonly IbkrOrderMapper _mapper = new();

	[Fact]
	public void Cash_market_order_maps_to_ib_codes() =>
		Assert.Equal(
			new Order
			{
				Account = "U0000001",
				Action = "BUY",
				OrderType = "MKT",
				Quantity = 0,
				CashQuantity = 10,
				TimeInForce = "IOC",
			},
			_mapper.ToMapped(
				new OrderDto
				{
					Account = "U0000001",
					Action = OrderAction.Buy,
					OrderType = OrderType.Market,
					CashQuantity = 10,
					TimeInForce = TimeInForce.Ioc,
				}
			)
		);

	[Fact]
	public void Limit_sell_maps_its_price_and_tif()
	{
		var order = _mapper.ToMapped(
			new OrderDto
			{
				Action = OrderAction.Sell,
				OrderType = OrderType.Limit,
				Quantity = 0.0002m,
				LimitPrice = 20000.5m,
				TimeInForce = TimeInForce.Gtc,
			}
		);

		Assert.Equal(
			("SELL", "LMT", 0.0002m, 20000.5, "GTC"),
			(
				order.Action,
				order.OrderType,
				order.Quantity,
				order.LimitPrice!.Value,
				order.TimeInForce
			)
		);
	}
}
