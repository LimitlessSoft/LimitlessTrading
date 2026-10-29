using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LSCore.Exceptions;
using LSCore.Validation.Contracts;
using LT.App.Helpers;
using LT.App.Stores;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Exceptions;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Orders;

namespace LT.App.Widgets.Chart;

/// <summary>
/// Working orders of the charted contract (selected account; <see cref="OpenOrdersStore"/>, one
/// reading for every chart): a line at each limit price and at the stop price of stop-type
/// orders. The limit line of this app's own limit orders drags to a new price, snapped to the
/// contract's minimum tick (<see cref="OrderTickSize"/>); dropping it asks to confirm
/// (<see cref="OrderMovePrompt"/>) and only Confirm sends the change
/// (<see cref="IOrderManager.ModifyOrderAsync"/>). Cancel or Esc puts the line back. A sent change
/// keeps the line at the new price until the orders are read again, which then show what IB
/// holds; a refusal puts it back at once and shows IB's reason. Orders placed in TWS or by
/// another client can't be changed from this connection (IB takes a change only from the client
/// that placed the order): their lines are drawn but don't move, and say why on hover.
/// </summary>
public partial class ChartViewModel
{
	private readonly IOrderManager _orderManager;
	private readonly OpenOrdersStore _openOrders;
	private readonly IDisposable _ordersWatch;

	// The contract the tick size was read for (or is being read for)
	private int _orderTickConId;

	// The price change being confirmed, sent, or sent and waiting for the next reading
	private PendingOrderMove? _pendingMove;

	/// <summary>The charted contract's working orders as lines (with a pending new price).</summary>
	[ObservableProperty]
	public partial IReadOnlyList<ChartOrderLineDto> OrderLines { get; set; } = [];

	/// <summary>The charted contract's minimum tick: dragged order lines snap to it.</summary>
	[ObservableProperty]
	public partial double OrderTickSize { get; set; } = FootprintConstants.FallbackTickSize;

	/// <summary>The change waiting for Confirm, in words; null when none.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsConfirmingOrderMove))]
	public partial string? OrderMovePrompt { get; private set; }

	/// <summary>A dropped order line waits for Confirm / Cancel.</summary>
	public bool IsConfirmingOrderMove => OrderMovePrompt != null;

	/// <summary>A confirmed change is on its way to IB.</summary>
	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(ConfirmOrderMoveCommand), nameof(RevertOrderMoveCommand))]
	public partial bool IsSendingOrderMove { get; private set; }

	/// <summary>
	/// An order line was dropped at <paramref name="price"/> (snapped to the tick by the chart):
	/// asks to confirm the change. Nothing is sent here.
	/// </summary>
	public void RequestOrderMove(string lineId, double price)
	{
		if (_pendingMove != null)
			return;
		if (
			OrderLines.FirstOrDefault(x => x.Id == lineId) is not { } line
			|| FindOrder(lineId) is not { } order
		)
			return;
		if (!line.CanMove)
		{
			ErrorMessage = line.MoveBlockedReason;
			return;
		}
		if (price <= 0 || price == order.LimitPrice)
			return;
		ErrorMessage = null;
		_pendingMove = new PendingOrderMove(order, lineId, price);
		OrderMovePrompt = OrderHelpers.DescribeModification(order, price);
		UpdateOrderLines();
	}

	/// <summary>
	/// Cancel / Esc: the line goes back and nothing is sent. False when there was nothing to
	/// cancel, or the change is already on its way.
	/// </summary>
	public bool CancelOrderMove()
	{
		if (_pendingMove is not { IsSent: false } || IsSendingOrderMove)
			return false;
		ClearOrderMove();
		return true;
	}

	[RelayCommand(CanExecute = nameof(CanAnswerOrderMove))]
	private void RevertOrderMove() => CancelOrderMove();

	/// <summary>Confirm: sends the new limit price to IB. This trades for real on a live account.</summary>
	[RelayCommand(CanExecute = nameof(CanAnswerOrderMove))]
	private async Task ConfirmOrderMoveAsync()
	{
		if (_pendingMove is not { IsSent: false } move || IsSendingOrderMove)
			return;
		IsSendingOrderMove = true;
		try
		{
			await _orderManager.ModifyOrderAsync(
				new ModifyOrderRequest { Order = move.Order, LimitPrice = (decimal)move.Price }
			);
			_messageLogManager.Add(
				MessageSeverity.Info,
				MessageLogConstants.SourceOrders,
				$"New limit price sent for order {move.Order.OrderId}: {OrderHelpers.DescribeModification(move.Order, move.Price)}"
			);
			// Taken: the line stays at the new price until the orders are read again
			if (_pendingMove == move)
			{
				_pendingMove = move with { IsSent = true };
				OrderMovePrompt = null;
			}
		}
		catch (Exception ex)
		{
			var reason = ErrorMessageHelpers.ToUserMessage(ex);
			// A refusal leaves the order as it was; no answer in time may still have changed it
			ErrorMessage = ex is IbkrRequestException or LSCoreBadRequestException
				? $"Order {move.Order.OrderId} not changed: {reason}"
				: reason;
			_messageLogManager.Add(
				MessageSeverity.Warning,
				MessageLogConstants.SourceOrders,
				$"New limit price for order {move.Order.OrderId} ({OrderHelpers.FormatPrice(move.Price)}): {ErrorMessage}"
			);
			if (_pendingMove == move)
				ClearOrderMove();
		}
		finally
		{
			IsSendingOrderMove = false;
		}
	}

	private bool CanAnswerOrderMove() => !IsSendingOrderMove;

	// UI thread: a new reading of the orders
	private void OnOpenOrdersChanged()
	{
		if (_disposed)
			return;
		if (_pendingMove is { } move)
		{
			var order = FindOrder(move.LineId);
			if (move.IsSent)
				// A reading that set out before the change may still show the old price: the
				// line shows what IB holds once a reading has the new price, or the one after
				_pendingMove =
					order == null || order.LimitPrice == move.Price || move.ReadingsSinceSent > 0
						? null
						: move with
						{
							ReadingsSinceSent = move.ReadingsSinceSent + 1,
						};
			else if (order == null || OrderHelpers.IsDone(order.Status))
			{
				ClearOrderMove();
				ErrorMessage = OrderValidationCodes.OVC_016.GetValidationMessage();
			}
			else if (order.LimitPrice != move.Order.LimitPrice && !IsSendingOrderMove)
			{
				// Changed elsewhere meanwhile: the prompt's old price would be wrong
				ClearOrderMove();
				ErrorMessage = $"Order {order.OrderId} changed meanwhile; drag its line again.";
			}
		}
		UpdateOrderLines();
	}

	private void ClearOrderMove()
	{
		_pendingMove = null;
		OrderMovePrompt = null;
		UpdateOrderLines();
	}

	// Another contract or a reload: a change not yet sent is dropped
	private void ResetOrderLines()
	{
		if (_pendingMove is { IsSent: false } && !IsSendingOrderMove)
		{
			_pendingMove = null;
			OrderMovePrompt = null;
		}
		UpdateOrderLines();
	}

	private void UpdateOrderLines()
	{
		var contract = _contract;
		OrderLines =
			contract == null
				? []
				: ChartOrderHelpers.GetOrderLines(
					_openOrders.Orders,
					contract.ConId,
					_accountStore.SelectedAccount,
					_orderManager.GetModifyBlockReason,
					_pendingMove is { } move ? (move.LineId, move.Price) : null
				);
		if (contract != null && OrderLines.Any(x => x.CanMove))
			LoadOrderTick(contract);
	}

	// Once per contract, from the cached contract details the chart's trading hours come from
	private void LoadOrderTick(ContractDto contract)
	{
		if (_orderTickConId == contract.ConId)
			return;
		_orderTickConId = contract.ConId;
		// Never another contract's tick meanwhile
		OrderTickSize = FootprintConstants.FallbackTickSize;
		_ = LoadOrderTickAsync(contract);
	}

	// The continuation comes back on the UI thread
	private async Task LoadOrderTickAsync(ContractDto contract)
	{
		double tick;
		try
		{
			tick = await _footprintManager.GetTickSizeAsync(contract);
		}
		catch (Exception)
		{
			// Read again on the next reading of the orders
			_orderTickConId = 0;
			return;
		}
		if (!_disposed && _contract?.ConId == contract.ConId)
			OrderTickSize = tick;
	}

	private OpenOrderDto? FindOrder(string lineId) =>
		_openOrders.Orders.FirstOrDefault(x =>
			ChartOrderHelpers.GetLineId(x, false) == lineId
			|| ChartOrderHelpers.GetLineId(x, true) == lineId
		);

	/// <summary>The order as read when its line was dropped, the line, its new limit price.</summary>
	private sealed record PendingOrderMove(
		OpenOrderDto Order,
		string LineId,
		double Price,
		bool IsSent = false,
		int ReadingsSinceSent = 0
	);
}
