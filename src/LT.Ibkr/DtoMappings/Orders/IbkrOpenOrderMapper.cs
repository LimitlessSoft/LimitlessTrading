using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Orders;
using Order = IBKR.Net.Order;

namespace LT.Ibkr.DtoMappings.Orders;

/// <summary>
/// A working order as IB reported it → the IBKR.Net order to send again for a change: side, total
/// quantity, type, prices, time in force and account as read. IB doesn't report the outside-hours
/// flag, so it stays off (the app never sets it) and there is no cash quantity (market orders
/// are never changed).
/// </summary>
public class IbkrOpenOrderMapper : ILSCoreMapper<OpenOrderDto, Order>
{
	public Order ToMapped(OpenOrderDto source) =>
		new()
		{
			Account = source.Account,
			Action = source.Action,
			Quantity = source.Quantity,
			OrderType = source.OrderType,
			LimitPrice = source.LimitPrice,
			AuxPrice = source.AuxPrice,
			TimeInForce = source.TimeInForce,
		};
}
