using LT.Contracts.Dtos.Orders;

namespace LT.Contracts.Requests.Orders;

/// <summary>
/// A new limit price for a working order of this app, e.g. its line dragged on a chart. The rest
/// of the order is sent again as <see cref="Order"/> last read it.
/// </summary>
public class ModifyOrderRequest
{
	/// <summary>The order as last read from IB (<see cref="Interfaces.IManagers.IOrderManager.GetOpenOrdersAsync"/>).</summary>
	public OpenOrderDto? Order { get; set; }
	public decimal? LimitPrice { get; set; }
}
