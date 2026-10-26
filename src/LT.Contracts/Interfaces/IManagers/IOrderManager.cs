using LT.Contracts.Dtos.Orders;
using LT.Contracts.Requests.Orders;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>Open orders of every client and TWS, and placing / cancelling orders.</summary>
public interface IOrderManager
{
	/// <summary>Working orders, with <see cref="OpenOrderDto.CanCancel"/> set for this app's own.</summary>
	Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(
		string? account,
		CancellationToken cancellationToken = default
	);

	/// <summary>Checks an order without sending it; throws the first problem as a bad request.</summary>
	void ValidateOrder(PlaceOrderRequest request);

	/// <summary>Validates and sends an order; this trades for real on a live account.</summary>
	Task<PlacedOrderDto> PlaceOrderAsync(
		PlaceOrderRequest request,
		CancellationToken cancellationToken = default
	);

	Task CancelOrderAsync(int orderId, CancellationToken cancellationToken = default);
}
