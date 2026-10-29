using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Orders;
using LT.Ibkr.DtoMappings.Orders;

namespace LT.Tests.Ibkr;

public class IbkrOpenOrderMapperTests
{
	[Fact]
	public void A_working_order_is_sent_again_as_ib_reported_it()
	{
		var order = new IbkrOpenOrderMapper().ToMapped(
			new OpenOrderDto
			{
				OrderId = 44,
				ClientId = 101,
				Account = "U0000001",
				Action = "SELL",
				Quantity = 3,
				Filled = 1,
				OrderType = "LMT",
				LimitPrice = 1.60,
				TimeInForce = "GTC",
				Contract = new ContractDto { ConId = 800000001 },
			}
		);

		Assert.Equal("U0000001", order.Account);
		Assert.Equal("SELL", order.Action);
		// The order's total, not what is left: IB keeps the filled part as filled
		Assert.Equal(3, order.Quantity);
		Assert.Equal("LMT", order.OrderType);
		Assert.Equal(1.60, order.LimitPrice);
		Assert.Null(order.AuxPrice);
		Assert.Equal("GTC", order.TimeInForce);
		Assert.False(order.OutsideRegularHours);
		Assert.Null(order.CashQuantity);
	}
}
