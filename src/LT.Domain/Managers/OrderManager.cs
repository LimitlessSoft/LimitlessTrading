using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Cache;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Orders;

namespace LT.Domain.Managers;

public class OrderManager(IIbkrApiClient ibkrApiClient, ICacheManager cacheManager) : IOrderManager
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
		var result = orders
			.Where(x => string.IsNullOrEmpty(account) || x.Account == account)
			.OrderBy(x => x.OrderId)
			.ToList();
		if (!string.IsNullOrEmpty(account))
			cacheManager.Save<IReadOnlyList<OpenOrderDto>>(
				CacheType.Snapshots,
				CacheHelpers.GetAccountKey(CacheConstants.OpenOrdersSnapshot, account),
				result
			);
		return result;
	}

	public CachedDto<IReadOnlyList<OpenOrderDto>>? GetOpenOrdersSnapshot(string account)
	{
		var snapshot = cacheManager.GetStale<IReadOnlyList<OpenOrderDto>>(
			CacheType.Snapshots,
			CacheHelpers.GetAccountKey(CacheConstants.OpenOrdersSnapshot, account)
		);
		// A remembered order may be gone already: nothing is actionable from a snapshot
		foreach (var order in snapshot?.Value ?? [])
			order.CanCancel = false;
		return snapshot;
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
