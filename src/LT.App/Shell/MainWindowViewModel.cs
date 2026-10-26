using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Pages.TradingPage;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.App.Widgets.ConnectionBar;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Connection;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Shell;

public partial class MainWindowViewModel : ViewModelBase
{
	private readonly IMarketDataManager _marketDataManager;
	private readonly ISettingsManager _settingsManager;
	private readonly AccountStore _accountStore;
	private readonly IConnectionManager _connectionManager;
	private readonly DisplayTimeStore _displayTime;

	public MainWindowViewModel(
		IConnectionManager connectionManager,
		IMarketDataManager marketDataManager,
		ISettingsManager settingsManager,
		AccountStore accountStore,
		AppearanceStore appearance,
		DisplayTimeStore displayTime,
		ConnectionBarViewModel connectionBar,
		TradingPageViewModel tradingPage,
		UpdateViewModel update
	)
	{
		_marketDataManager = marketDataManager;
		_settingsManager = settingsManager;
		_accountStore = accountStore;
		_connectionManager = connectionManager;
		_displayTime = displayTime;
		ConnectionBar = connectionBar;
		Appearance = appearance;
		Update = update;
		CurrentPage = tradingPage;

		Apply(connectionManager.Status);
		connectionManager.StatusChanged += status => Dispatcher.UIThread.Post(() => Apply(status));
		accountStore.PropertyChanged += (_, _) => UpdateAccountText();
		displayTime.ZoneChanged += () => Apply(_connectionManager.Status);
		settingsManager.AppSettingsChanged += _ => Dispatcher.UIThread.Post(UpdateStatusBar);
		new DispatcherTimer(
			TimeSpan.FromSeconds(1),
			DispatcherPriority.Background,
			(_, _) => UpdateStatusBar()
		).Start();
		UpdateStatusBar();
	}

	public string Title => "LimitlessTrading";

	public ConnectionBarViewModel ConnectionBar { get; }

	/// <summary>Cards or flush: the window carries the flush class the styles key on.</summary>
	public AppearanceStore Appearance { get; }

	public UpdateViewModel Update { get; }

	public TradingPageViewModel CurrentPage { get; }

	[ObservableProperty]
	public partial string AccountsText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string ServerTimeText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string MarketDataText { get; set; } = string.Empty;

	public LayoutDto GetLayout() => _settingsManager.GetLayout();

	public void SaveLayout(double? width, double? height, bool isMaximized)
	{
		var layout = _settingsManager.GetLayout();
		if (width is { } w && height is { } h)
		{
			layout.WindowWidth = w;
			layout.WindowHeight = h;
		}
		layout.IsMaximized = isMaximized;
		CurrentPage.FillLayout(layout);
		_settingsManager.SaveLayout(layout);
	}

	[RelayCommand]
	public void RefreshAll() => CurrentPage.RefreshAll();

	public void ShowTool(WorkspaceTool tool) => CurrentPage.ShowTool(tool);

	/// <summary>Ctrl+Alt+key: shows a single-instance widget and focuses it.</summary>
	public void FocusTool(WorkspaceTool tool) => CurrentPage.FocusTool(tool);

	public void ToggleTool(WorkspaceTool tool) => CurrentPage.ToggleTool(tool);

	/// <summary>Ctrl+Tab / Ctrl+Shift+Tab: the next or previous center view.</summary>
	public void CycleView(int step) => CurrentPage.CycleView(step);

	/// <summary>Ctrl+Shift+F: the current widget over the whole window, or back.</summary>
	public void ToggleFullscreen() => CurrentPage.ToggleFullscreen();

	private void Apply(ConnectionStatusDto status)
	{
		ServerTimeText =
			status.State == ConnectionState.Connected && status.ServerTime is { } serverTime
				? $"Server time at connect {_displayTime.Format(serverTime)}"
				: string.Empty;
		UpdateAccountText();
	}

	private void UpdateAccountText() =>
		AccountsText = _accountStore.SelectedAccount is { } account
			? $"Account {account} ({_accountStore.Accounts.Count} managed)"
			: "No account";

	private void UpdateStatusBar() =>
		MarketDataText =
			$"Market data: {_settingsManager.GetAppSettings().MarketDataType} · lines {_marketDataManager.ActiveLines}/{MarketDataConstants.MaxLines}";
}
