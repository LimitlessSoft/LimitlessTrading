using IBKR.Net;
using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Orders;

namespace LT.Ibkr.DtoMappings.Orders;

public class OrderStatusDtoMapper : ILSCoreMapper<OrderStatus, OrderStatusDto>
{
	public OrderStatusDto ToMapped(OrderStatus source) =>
		new()
		{
			OrderId = source.OrderId,
			Status = source.Status,
			Filled = source.Filled,
			Remaining = source.Remaining,
			AverageFillPrice = source.AverageFillPrice,
			LastFillPrice = source.LastFillPrice,
			WhyHeld = source.WhyHeld,
			IsDone = source.IsDone,
		};
}
