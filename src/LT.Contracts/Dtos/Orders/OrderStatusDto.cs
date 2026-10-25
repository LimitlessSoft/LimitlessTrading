namespace LT.Contracts.Dtos.Orders;

/// <summary>An order's state as IB reports it (Submitted, Filled, Cancelled …).</summary>
public class OrderStatusDto
{
	public int OrderId { get; set; }
	public string Status { get; set; } = string.Empty;
	public decimal Filled { get; set; }
	public decimal Remaining { get; set; }
	public double AverageFillPrice { get; set; }
	public double LastFillPrice { get; set; }
	public string WhyHeld { get; set; } = string.Empty;

	/// <summary>No more updates follow (Filled, Cancelled, ApiCancelled, Inactive).</summary>
	public bool IsDone { get; set; }
}
