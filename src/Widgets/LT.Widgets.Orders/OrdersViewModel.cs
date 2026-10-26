using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.App.Workspace;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Widgets.Orders;

/// <summary>Working orders of every client and TWS; this app's own can be cancelled here.</summary>
public partial class OrdersViewModel : ConnectedViewModelBase, ILinkable, ITickerDragSource
{
	private readonly IOrderManager _orderManager;
	private readonly IMessageLogManager _messageLogManager;
	private readonly AccountStore _accountStore;
	private bool _restoringSelection;
	private readonly DisplayTimeStore _displayTime;
	private readonly ZonedText _updatedText;
	private readonly ZonedText _staleText;

	public OrdersViewModel(
		IOrderManager orderManager,
		IConnectionManager connectionManager,
		IMessageLogManager messageLogManager,
		AccountStore accountStore,
		LinkGroupStore linkGroupStore,
		DisplayTimeStore displayTime
	)
		: base(connectionManager)
	{
		_orderManager = orderManager;
		_messageLogManager = messageLogManager;
		_accountStore = accountStore;
		_displayTime = displayTime;
		_updatedText = new ZonedText(x => UpdatedText = x);
		_staleText = new ZonedText(x => StaleText = x);
		Link = new ContractLink(linkGroupStore, () => GetLinkContract(SelectedOrder));
		accountStore.PropertyChanged += OnAccountChanged;
		displayTime.ZoneChanged += OnZoneChanged;
		// After the base class has applied the connection state
		Dispatcher.UIThread.Post(() =>
		{
			if (!IsConnected)
				ShowSnapshot(null);
		});
	}

	/// <summary>Clicking an order sends its contract to the link group.</summary>
	public ContractLink Link { get; }

	public ObservableCollection<OpenOrderDto> Orders { get; } = [];

	[ObservableProperty]
	public partial OpenOrderDto? SelectedOrder { get; set; }

	[ObservableProperty]
	public partial string? UpdatedText { get; set; }

	/// <summary>Set while the list is the account's last-known orders, not live (none cancellable).</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsStale))]
	public partial string? StaleText { get; set; }

	public bool IsStale => StaleText != null;

	partial void OnSelectedOrderChanged(OpenOrderDto? value)
	{
		// A reload putting the same order back selected must not pull the group back to it
		if (!_restoringSelection && GetLinkContract(value) is { } contract)
			Link.Publish(contract);
	}

	[RelayCommand]
	public void Refresh() => RestartSession();

	public override void Dispose()
	{
		_accountStore.PropertyChanged -= OnAccountChanged;
		_displayTime.ZoneChanged -= OnZoneChanged;
		Link.Dispose();
		base.Dispose();
	}

	/// <summary>Asks IB to cancel; the reload right after shows PendingCancel, then it drops off.</summary>
	[RelayCommand]
	private async Task CancelOrderAsync(OpenOrderDto order)
	{
		// Never from a snapshot: a remembered order may already be filled or gone
		if (!order.CanCancel || IsStale)
			return;
		try
		{
			await _orderManager.CancelOrderAsync(order.OrderId);
			_messageLogManager.Add(
				MessageSeverity.Info,
				MessageLogConstants.SourceOrders,
				$"Cancel requested for order {order.OrderId} ({order.Action} {order.Quantity:#,0.####} {order.Symbol})"
			);
			RestartSession();
		}
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
	}

	protected override void OnConnected(CancellationToken session) =>
		_ = RunPeriodicAsync(LoadAsync, AccountConstants.OrdersRefreshInterval, session);

	protected override void OnDisconnected() => ShowSnapshot(null);

	private void OnZoneChanged()
	{
		_updatedText.Rebuild();
		_staleText.Rebuild();
	}

	private void OnAccountChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(AccountStore.SelectedAccount))
			return;
		if (IsConnected)
			RestartSession();
		else
			ShowSnapshot(null);
	}

	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		IReadOnlyList<OpenOrderDto> orders;
		try
		{
			orders = await _orderManager.GetOpenOrdersAsync(
				_accountStore.SelectedAccount,
				cancellationToken
			);
		}
		catch (Exception ex) when (ex is not OperationCanceledException && Orders.Count == 0)
		{
			// Nothing live shown yet: the last-known orders, marked, beat an empty list
			ShowSnapshot(ErrorMessageHelpers.ToUserMessage(ex));
			throw;
		}
		_staleText.Clear();
		var selected = SelectedOrder?.PermId;
		Orders.Clear();
		foreach (var order in orders)
			Orders.Add(order);
		var updatedAt = _displayTime.Now;
		_updatedText.Set(() => $"{orders.Count} open · updated {_displayTime.Format(updatedAt)}");
		RestoreSelection(selected);
	}

	// The list is rebuilt on every refresh; the same order (by permId) stays selected
	private void RestoreSelection(long? permId)
	{
		_restoringSelection = true;
		try
		{
			SelectedOrder = Orders.FirstOrDefault(x => x.PermId == permId);
		}
		finally
		{
			_restoringSelection = false;
		}
	}

	/// <summary>An order row dragged onto a chart: the same contract a click sends to the group.</summary>
	public ContractDto? GetDragContract(object? item) => GetLinkContract(item as OpenOrderDto);

	// Remembered orders are for looking at only, like remembered positions; they drive no other widget
	private ContractDto? GetLinkContract(OpenOrderDto? order) =>
		order is { Contract.ConId: > 0 } && !IsStale ? order.Contract : null;

	// Display only: snapshot orders are never cancellable and never feed an order decision
	private void ShowSnapshot(string? error)
	{
		Orders.Clear();
		_staleText.Clear();
		_updatedText.Clear();
		if (_accountStore.SnapshotAccount is not { } account)
			return;
		if (_orderManager.GetOpenOrdersSnapshot(account) is not { } snapshot)
			return;
		foreach (var order in snapshot.Value)
			Orders.Add(order);
		_updatedText.SetFixed($"{snapshot.Value.Count} open");
		_staleText.Set(() =>
			CacheHelpers.FormatStale(
				_displayTime.ToDisplay(snapshot.FetchedAt),
				_displayTime.Now,
				account,
				error
			)
		);
	}
}
