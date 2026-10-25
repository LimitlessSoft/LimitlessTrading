namespace LT.Contracts.Dtos.Orders;

/// <summary>
/// A sent order: its id (to cancel it) and its status updates until it is done; a rejection fails
/// the stream. Stopping reading never cancels the order.
/// </summary>
public class PlacedOrderDto
{
	public int OrderId { get; init; }
	public required IAsyncEnumerable<OrderStatusDto> Statuses { get; init; }
}
