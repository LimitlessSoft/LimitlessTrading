using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Pages.TradingPage;
using LT.App.ViewModels;
using LT.App.Widgets.ConnectionBar;
using LT.Contracts.Dtos.Connection;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Shell;

public partial class MainWindowViewModel : ViewModelBase
{
	public MainWindowViewModel(
		IConnectionManager connectionManager,
		ConnectionBarViewModel connectionBar,
		TradingPageViewModel tradingPage
	)
	{
		ConnectionBar = connectionBar;
		CurrentPage = tradingPage;

		Apply(connectionManager.Status);
		connectionManager.StatusChanged += status => Dispatcher.UIThread.Post(() => Apply(status));
	}

	public string Title => "LimitlessTrading";

	public ConnectionBarViewModel ConnectionBar { get; }

	public TradingPageViewModel CurrentPage { get; }

	[ObservableProperty]
	public partial string AccountsText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string ServerTimeText { get; set; } = string.Empty;

	private void Apply(ConnectionStatusDto status)
	{
		var connected = status.State == ConnectionState.Connected;
		AccountsText = connected ? $"Accounts: {string.Join(", ", status.Accounts)}" : "No account";
		ServerTimeText =
			connected && status.ServerTime is { } serverTime
				? $"Server time at connect: {serverTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}"
				: string.Empty;
	}
}
