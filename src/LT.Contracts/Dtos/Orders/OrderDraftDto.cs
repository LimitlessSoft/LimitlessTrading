using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Orders;

/// <summary>
/// What an order ticket opens pre-filled with (e.g. closing a position). Only a starting point:
/// the user still reviews and sends it from the ticket.
/// </summary>
public class OrderDraftDto
{
	public ContractDto Contract { get; init; } = new();
	public string Account { get; init; } = string.Empty;
	public OrderAction Action { get; init; }
	public decimal Quantity { get; init; }

	/// <summary>
	/// Put in the ticket's limit price box (e.g. an option's mid), so switching to a limit order
	/// starts from it; the order type stays market. Null leaves the box empty.
	/// </summary>
	public decimal? LimitPrice { get; init; }

	/// <summary>Why the ticket is pre-filled, shown above it; null for none.</summary>
	public string? Note { get; init; }
}
