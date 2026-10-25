using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Enums;
using Order = IBKR.Net.Order;

namespace LT.Ibkr.DtoMappings.Orders;

/// <summary>LT order → IBKR.Net order: IB's codes for side, type and time in force.</summary>
public class IbkrOrderMapper : ILSCoreMapper<OrderDto, Order>
{
	public Order ToMapped(OrderDto source) =>
		new()
		{
			Account = source.Account,
			Action = source.Action switch
			{
				OrderAction.Buy => "BUY",
				OrderAction.Sell => "SELL",
				_ => throw new ArgumentOutOfRangeException(nameof(source), source.Action, null),
			},
			OrderType = source.OrderType switch
			{
				OrderType.Market => "MKT",
				OrderType.Limit => "LMT",
				_ => throw new ArgumentOutOfRangeException(nameof(source), source.OrderType, null),
			},
			Quantity = source.Quantity,
			CashQuantity = (double?)source.CashQuantity,
			LimitPrice = (double?)source.LimitPrice,
			TimeInForce = source.TimeInForce switch
			{
				TimeInForce.Day => "DAY",
				TimeInForce.Gtc => "GTC",
				TimeInForce.Ioc => "IOC",
				_ => throw new ArgumentOutOfRangeException(
					nameof(source),
					source.TimeInForce,
					null
				),
			},
			OutsideRegularHours = source.OutsideRegularHours,
		};
}
