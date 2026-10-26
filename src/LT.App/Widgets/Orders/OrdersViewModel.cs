using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Enums;
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
	}

	public ObservableCollection<OpenOrderDto> Orders { get; } = [];

	[ObservableProperty]
	public partial string? UpdatedText { get; set; }

	[RelayCommand]
	public void Refresh() => RestartSession();

	/// <summary>Asks IB to cancel; the reload right after shows PendingCancel, then it drops off.</summary>
	[RelayCommand]
	private async Task CancelOrderAsync(OpenOrderDto order)
	{
		if (!order.CanCancel)
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

	protected override void OnDisconnected() => Orders.Clear();

	private void OnAccountChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(AccountStore.SelectedAccount))
			RestartSession();
	}

	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		var orders = await _orderManager.GetOpenOrdersAsync(
			_accountStore.SelectedAccount,
			cancellationToken
		);
		Orders.Clear();
		foreach (var order in orders)
			Orders.Add(order);
		UpdatedText = $"{orders.Count} open · updated {DateTime.Now:HH:mm:ss}";
	}
}
