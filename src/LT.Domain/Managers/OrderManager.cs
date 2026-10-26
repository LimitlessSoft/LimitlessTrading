using LSCore.Validation.Domain;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Orders;

namespace LT.Domain.Managers;

public class OrderManager(IIbkrApiClient ibkrApiClient) : IOrderManager
{
	public async Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(
		string? account,
		CancellationToken cancellationToken = default
	)
	{
		if (!ibkrApiClient.IsConnected)
			return [];

		var orders = await ibkrApiClient.GetOpenOrdersAsync(cancellationToken);
		foreach (var order in orders)
			order.CanCancel = OrderHelpers.CanCancel(order, ibkrApiClient.OrderClientId);
		return orders
			.Where(x => string.IsNullOrEmpty(account) || x.Account == account)
			.OrderBy(x => x.OrderId)
			.ToList();
	}

	public void ValidateOrder(PlaceOrderRequest request) => request.Validate();

	public Task<PlacedOrderDto> PlaceOrderAsync(
		PlaceOrderRequest request,
		CancellationToken cancellationToken = default
	)
	{
		request.Validate();
		return ibkrApiClient.PlaceOrderAsync(
			request.Contract!,
			OrderHelpers.ToOrder(request),
			cancellationToken
		);
	}

	public Task CancelOrderAsync(int orderId, CancellationToken cancellationToken = default) =>
		ibkrApiClient.CancelOrderAsync(orderId, cancellationToken);
}
