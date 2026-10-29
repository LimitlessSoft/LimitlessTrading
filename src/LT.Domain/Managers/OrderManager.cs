using System.Collections.Concurrent;
using LSCore.Exceptions;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Cache;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Dtos.Positions;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Orders;

namespace LT.Domain.Managers;

public class OrderManager(IIbkrApiClient ibkrApiClient, ICacheManager cacheManager) : IOrderManager
{
	// Positions with a quick exit still working, so a second click can't send a second exit
	private readonly ConcurrentDictionary<(string Account, int ContractId), byte> _exits = new();

	public event Action<OrderStatusDto>? OrderFilled;

	public event Action? OrdersChanged;

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

	public async Task<PlacedOrderDto> PlaceOrderAsync(
		PlaceOrderRequest request,
		CancellationToken cancellationToken = default
	)
	{
		request.Validate();
		var placed = await ibkrApiClient.PlaceOrderAsync(
			request.Contract!,
			OrderHelpers.ToOrder(request),
			cancellationToken
		);
		return new PlacedOrderDto
		{
			OrderId = placed.OrderId,
			Statuses = OrderHelpers.OnFillsAsync(
				AsyncEnumerableHelpers.OnEachAsync(placed.Statuses, _ => OrdersChanged?.Invoke()),
				x => OrderFilled?.Invoke(x)
			),
		};
	}

	public async Task<PlacedOrderDto> ExitPositionAsync(
		PositionDto position,
		CancellationToken cancellationToken = default
	)
	{
		var request =
			OrderHelpers.GetQuickExitRequest(position)
			?? throw new LSCoreBadRequestException(
				OrderValidationCodes.OVC_010.GetValidationMessage()
			);
		request.Validate();
		var key = (position.Account, position.ContractId);
		if (!_exits.TryAdd(key, 0))
			throw new LSCoreBadRequestException(
				OrderValidationCodes.OVC_011.GetValidationMessage()
			);
		try
		{
			var placed = await ibkrApiClient.PlaceOrderAsync(
				request.Contract!,
				OrderHelpers.ToOrder(request),
				cancellationToken
			);
			return new PlacedOrderDto
			{
				OrderId = placed.OrderId,
				Statuses = AsyncEnumerableHelpers.OnEndAsync(
					OrderHelpers.OnFillsAsync(
						AsyncEnumerableHelpers.OnEachAsync(
							placed.Statuses,
							_ => OrdersChanged?.Invoke()
						),
						x => OrderFilled?.Invoke(x)
					),
					() => _exits.TryRemove(key, out _)
				),
			};
		}
		catch
		{
			_exits.TryRemove(key, out _);
			throw;
		}
	}

	public string? GetModifyBlockReason(OpenOrderDto order) =>
		OrderHelpers.GetModifyBlock(order, ibkrApiClient.OrderClientId)?.GetValidationMessage();

	public async Task ModifyOrderAsync(
		ModifyOrderRequest request,
		CancellationToken cancellationToken = default
	)
	{
		request.Validate();
		var order = request.Order!;
		if (OrderHelpers.GetModifyBlock(order, ibkrApiClient.OrderClientId) is { } block)
			throw new LSCoreBadRequestException(block.GetValidationMessage());
		try
		{
			await ibkrApiClient.ModifyOrderAsync(
				order,
				request.LimitPrice!.Value,
				cancellationToken
			);
		}
		finally
		{
			// Taken, refused or unanswered: what IB holds now is what the orders show
			OrdersChanged?.Invoke();
		}
	}

	public async Task CancelOrderAsync(int orderId, CancellationToken cancellationToken = default)
	{
		await ibkrApiClient.CancelOrderAsync(orderId, cancellationToken);
		OrdersChanged?.Invoke();
	}
}
