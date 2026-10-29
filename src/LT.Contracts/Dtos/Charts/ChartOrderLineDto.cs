namespace LT.Contracts.Dtos.Charts;

/// <summary>
/// A working order's price drawn on a chart: its limit price, or the stop (trigger) price of a
/// stop-type order; an order with both has two lines.
/// </summary>
public class ChartOrderLineDto
{
	/// <summary>Tells the line apart across reloads: the order (perm id, client, id) and which price.</summary>
	public string Id { get; init; } = string.Empty;
	public int OrderId { get; init; }
	public long PermId { get; init; }

	/// <summary>BUY or SELL, as IB reports it.</summary>
	public string Action { get; init; } = string.Empty;
	public bool IsBuy { get; init; }

	/// <summary>What is left to fill.</summary>
	public decimal Quantity { get; init; }

	/// <summary>IB's order type (LMT, STP, STP LMT …).</summary>
	public string OrderType { get; init; } = string.Empty;

	/// <summary>Where the line is drawn: the order's price, or the new one while it is pending.</summary>
	public double Price { get; init; }

	/// <summary>The stop (trigger) price, not the limit price.</summary>
	public bool IsStopPrice { get; init; }

	/// <summary>Null when the line can be dragged to a new price; otherwise why not.</summary>
	public string? MoveBlockedReason { get; init; }

	public bool CanMove => MoveBlockedReason == null;

	/// <summary>
	/// A new price is being confirmed or sent: the line is at it, and this is the order's price
	/// as IB last reported it. Null otherwise.
	/// </summary>
	public double? PendingFromPrice { get; init; }

	public bool IsPending => PendingFromPrice != null;
}
