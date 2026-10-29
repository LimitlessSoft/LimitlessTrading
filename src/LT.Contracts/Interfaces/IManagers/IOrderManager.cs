using LT.Contracts.Dtos.Cache;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Dtos.Positions;
using LT.Contracts.Requests.Orders;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>Open orders of every client and TWS, and placing / cancelling orders.</summary>
public interface IOrderManager
{
	/// <summary>
	/// Raised on any thread when an order placed through this manager reports a new fill
	/// (<see cref="Helpers.OrderHelpers.IsNewFill"/>), while its
	/// <see cref="PlacedOrderDto.Statuses"/> are being read.
	/// </summary>
	event Action<OrderStatusDto>? OrderFilled;

	/// <summary>
	/// Raised on any thread when this app's orders may have changed, so open orders are worth
	/// reading again: a status of an order placed through this manager (while its
	/// <see cref="PlacedOrderDto.Statuses"/> are read), a cancel or a price change sent (also a
	/// refused one).
	/// </summary>
	event Action? OrdersChanged;

	/// <summary>
	/// Working orders, with <see cref="OpenOrderDto.CanCancel"/> set for this app's own. Keeps the
	/// account's last-known orders as a snapshot.
	/// </summary>
	Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(
		string? account,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// The account's last-known open orders (always IsStale, none cancellable), for display only
	/// while live data is unavailable; never for decisions. Null when there is none.
	/// </summary>
	CachedDto<IReadOnlyList<OpenOrderDto>>? GetOpenOrdersSnapshot(string account);

	/// <summary>Checks an order without sending it; throws the first problem as a bad request.</summary>
	void ValidateOrder(PlaceOrderRequest request);

	/// <summary>Validates and sends an order; this trades for real on a live account.</summary>
	Task<PlacedOrderDto> PlaceOrderAsync(
		PlaceOrderRequest request,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Quick exit: sends a market order for the whole of <paramref name="position"/> right away,
	/// with no review (<see cref="Helpers.OrderHelpers.GetQuickExitRequest"/>); this trades for
	/// real on a live account. Throws a bad request for a flat position, and while an earlier exit
	/// of the same position is still working: that lasts until its
	/// <see cref="PlacedOrderDto.Statuses"/> end, so read them to the end (or stop reading).
	/// </summary>
	Task<PlacedOrderDto> ExitPositionAsync(
		PositionDto position,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Why the price of <paramref name="order"/> can't be changed from this app (e.g. it was placed
	/// in TWS), as a message for the user; null when it can (<see cref="ModifyOrderAsync"/>).
	/// </summary>
	string? GetModifyBlockReason(OpenOrderDto order);

	/// <summary>
	/// Validates and sends a new limit price for a working order of this app; this trades for real
	/// on a live account. Completes when IB has taken it. IB's refusal (e.g. 110, a price off the
	/// tick) throws with IB's text and leaves the order as it was; no answer in time throws
	/// <see cref="TimeoutException"/> (the change may still go through: read the orders again).
	/// </summary>
	Task ModifyOrderAsync(
		ModifyOrderRequest request,
		CancellationToken cancellationToken = default
	);

	Task CancelOrderAsync(int orderId, CancellationToken cancellationToken = default);
}
