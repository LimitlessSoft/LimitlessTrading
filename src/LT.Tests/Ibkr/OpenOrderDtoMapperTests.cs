using IBKR.Net;
using LT.Ibkr.DtoMappings.Orders;
using LT.Tests.Fakes;

namespace LT.Tests.Ibkr;

public class OpenOrderDtoMapperTests
{
	public OpenOrderDtoMapperTests() => LSCoreContainer.EnsureInitialized();

	[Fact]
	public void An_open_order_maps_its_order_and_status()
	{
		var dto = new OpenOrderDtoMapper().ToMapped(
			new OpenOrder(
				200,
				101,
				900000001,
				new Contract
				{
					ConId = 14232,
					Symbol = "VOW",
					SecurityType = "STK",
					Exchange = "SMART",
					Currency = "EUR",
				},
				new Order
				{
					Action = "BUY",
					Quantity = 1,
					OrderType = "LMT",
					LimitPrice = 66,
					TimeInForce = "DAY",
					Account = "U0000001",
				},
				"Submitted",
				0,
				1
			)
		);

		Assert.Equal(200, dto.OrderId);
		Assert.Equal(900000001, dto.PermId);
		Assert.Equal("U0000001", dto.Account);
		Assert.Equal("VOW", dto.Symbol);
		Assert.Equal("STK", dto.SecurityType);
		Assert.Equal("BUY", dto.Action);
		Assert.Equal(1, dto.Quantity);
		Assert.Equal("LMT", dto.OrderType);
		Assert.Equal(66, dto.LimitPrice);
		Assert.Null(dto.AuxPrice);
		Assert.Equal("DAY", dto.TimeInForce);
		Assert.Equal("Submitted", dto.Status);
	}
}
