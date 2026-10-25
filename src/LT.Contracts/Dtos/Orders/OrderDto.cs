using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Orders;

/// <summary>An order to send; <see cref="CashQuantity"/> replaces <see cref="Quantity"/> when set.</summary>
public class OrderDto
{
	public string Account { get; set; } = string.Empty;
	public OrderAction Action { get; set; }
	public OrderType OrderType { get; set; }
	public decimal Quantity { get; set; }
	public decimal? CashQuantity { get; set; }
	public decimal? LimitPrice { get; set; }
	public TimeInForce TimeInForce { get; set; }
	public bool OutsideRegularHours { get; set; }
}
