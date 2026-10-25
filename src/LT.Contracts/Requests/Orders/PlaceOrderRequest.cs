using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;

namespace LT.Contracts.Requests.Orders;

/// <summary>
/// An order from the ticket: a <see cref="Quantity"/>, or for a market order a
/// <see cref="CashAmount"/> of money to spend (e.g. $10 of BTC).
/// </summary>
public class PlaceOrderRequest
{
	public ContractDto? Contract { get; set; }
	public string Account { get; set; } = string.Empty;
	public OrderAction Action { get; set; }
	public OrderType OrderType { get; set; }
	public decimal? Quantity { get; set; }
	public decimal? CashAmount { get; set; }
	public decimal? LimitPrice { get; set; }
	public TimeInForce TimeInForce { get; set; }
	public bool OutsideRegularHours { get; set; }
}
