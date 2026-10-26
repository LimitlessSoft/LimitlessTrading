using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Widgets.Orders;

/// <summary>Working orders of every client and TWS; this app's own can be cancelled here.</summary>
public partial class OrdersViewModel : ConnectedViewModelBase
{
	private readonly IOrderManager _orderManager;
	private readonly IMessageLogManager _messageLogManager;
	private readonly AccountStore _accountStore;

	public OrdersViewModel(
		IOrderManager orderManager,
		IConnectionManager connectionManager,
		IMessageLogManager messageLogManager,
		AccountStore accountStore
	)
		: base(connectionManager)
	{
		_orderManager = orderManager;
		_messageLogManager = messageLogManager;
		_accountStore = accountStore;
		accountStore.PropertyChanged += OnAccountChanged;
		// After the base class has applied the connection state
		Dispatcher.UIThread.Post(() =>
		{
			if (!IsConnected)
				ShowSnapshot(null);
		});
	}

	public ObservableCollection<OpenOrderDto> Orders { get; } = [];

	[ObservableProperty]
	public partial string? UpdatedText { get; set; }

	/// <summary>Set while the list is the account's last-known orders, not live (none cancellable).</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsStale))]
	public partial string? StaleText { get; set; }

	public bool IsStale => StaleText != null;

	[RelayCommand]
	public void Refresh() => RestartSession();

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
		StaleText = null;
		Orders.Clear();
		foreach (var order in orders)
			Orders.Add(order);
		UpdatedText = $"{orders.Count} open · updated {DateTime.Now:HH:mm:ss}";
	}

	// Display only: snapshot orders are never cancellable and never feed an order decision
	private void ShowSnapshot(string? error)
	{
		Orders.Clear();
		StaleText = null;
		UpdatedText = null;
		if (_accountStore.SnapshotAccount is not { } account)
			return;
		if (_orderManager.GetOpenOrdersSnapshot(account) is not { } snapshot)
			return;
		foreach (var order in snapshot.Value)
			Orders.Add(order);
		UpdatedText = $"{snapshot.Value.Count} open";
		StaleText = CacheHelpers.FormatStale(
			snapshot.FetchedAt.ToLocalTime(),
			DateTimeOffset.Now,
			account,
			error
		);
	}
}
