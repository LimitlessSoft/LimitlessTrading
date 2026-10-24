namespace LT.Contracts.Dtos.Orders;

public class OpenOrderDto
{
	public int OrderId { get; set; }
	public long PermId { get; set; }
	public string Account { get; set; } = string.Empty;
	public string Symbol { get; set; } = string.Empty;
	public string SecurityType { get; set; } = string.Empty;
	public string Action { get; set; } = string.Empty;
	public decimal Quantity { get; set; }
	public decimal Filled { get; set; }
	public string OrderType { get; set; } = string.Empty;
	public double? LimitPrice { get; set; }
	public double? AuxPrice { get; set; }
	public string TimeInForce { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
}
