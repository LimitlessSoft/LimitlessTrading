using LT.Contracts.Dtos.Contracts;

namespace LT.Contracts.Dtos.Orders;

public class OpenOrderDto
{
	public int OrderId { get; set; }

	/// <summary>The API client that placed it; 0 for TWS.</summary>
	public int ClientId { get; set; }
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

	/// <summary>Placed by this app and still working: the Orders widget can cancel it.</summary>
	public bool CanCancel { get; set; }

	/// <summary>The order's contract, sent to the link group; empty (ConId 0) in older snapshots.</summary>
	public ContractDto Contract { get; set; } = new();
}
