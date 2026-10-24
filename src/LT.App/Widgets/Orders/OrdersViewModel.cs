using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Widgets.Orders;

/// <summary>Open orders, read-only (this build cannot cancel or modify).</summary>
public partial class OrdersViewModel : ConnectedViewModelBase
{
	private readonly IOrderManager _orderManager;
	private readonly AccountStore _accountStore;

	public OrdersViewModel(
		IOrderManager orderManager,
		IConnectionManager connectionManager,
		AccountStore accountStore
	)
		: base(connectionManager)
	{
		_orderManager = orderManager;
		_accountStore = accountStore;
		accountStore.PropertyChanged += OnAccountChanged;
	}

	public ObservableCollection<OpenOrderDto> Orders { get; } = [];

	[ObservableProperty]
	public partial string? UpdatedText { get; set; }

	[RelayCommand]
	public void Refresh() => RestartSession();

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
