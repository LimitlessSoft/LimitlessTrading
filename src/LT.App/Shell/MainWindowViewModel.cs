using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Pages.TradingPage;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.App.Widgets.ConnectionBar;
using LT.App.Workspace;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Connection;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Shell;

public partial class MainWindowViewModel : ViewModelBase
{
	private readonly IMarketDataManager _marketDataManager;
	private readonly ISettingsManager _settingsManager;
	private readonly AccountStore _accountStore;
	private readonly IConnectionManager _connectionManager;
	private readonly DisplayTimeStore _displayTime;

	// 1 while a stream list refresh is posted: a burst of lines (a watchlist opening) posts one
	private int _streamsRefreshPosted;

	public MainWindowViewModel(
		IConnectionManager connectionManager,
		IMarketDataManager marketDataManager,
		ISettingsManager settingsManager,
		AccountStore accountStore,
		AppearanceStore appearance,
		DisplayTimeStore displayTime,
		PrivacyStore privacy,
		ConnectionBarViewModel connectionBar,
		TradingPageViewModel tradingPage,
		UpdateViewModel update,
		WorkspaceWindows windows
	)
	{
		Windows = windows;
		_marketDataManager = marketDataManager;
		_settingsManager = settingsManager;
		_accountStore = accountStore;
		_connectionManager = connectionManager;
		_displayTime = displayTime;
		ConnectionBar = connectionBar;
		Appearance = appearance;
		Privacy = privacy;
		Update = update;
		CurrentPage = tradingPage;

		Apply(connectionManager.Status);
		connectionManager.StatusChanged += status => Dispatcher.UIThread.Post(() => Apply(status));
		accountStore.PropertyChanged += (_, _) => UpdateAccountText();
		displayTime.ZoneChanged += () => Apply(_connectionManager.Status);
		settingsManager.AppSettingsChanged += _ => Dispatcher.UIThread.Post(UpdateStatusBar);
		marketDataManager.StreamsChanged += OnStreamsChanged;
		new DispatcherTimer(
			TimeSpan.FromSeconds(1),
			DispatcherPriority.Background,
			(_, _) =>
			{
				UpdateStatusBar();
				UpdateStreamAges();
			}
		).Start();
		RefreshStreams();
	}

	public string Title => "LimitlessTrading";

	public ConnectionBarViewModel ConnectionBar { get; }

	/// <summary>Cards or flush: the window carries the flush class the styles key on.</summary>
	public AppearanceStore Appearance { get; }

	/// <summary>Privacy mode, switched by the eye in the title bar.</summary>
	public PrivacyStore Privacy { get; }

	public UpdateViewModel Update { get; }

	public TradingPageViewModel CurrentPage { get; }

	/// <summary>The extra workspace windows; the main window opens and closes their windows.</summary>
	public WorkspaceWindows Windows { get; }

	[ObservableProperty]
	public partial string AccountsText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string ServerTimeText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string MarketDataText { get; set; } = string.Empty;

	/// <summary>The market data lines in use, oldest first (the status bar item's flyout).</summary>
	public ObservableCollection<MarketDataStreamRowViewModel> MarketDataStreams { get; } = [];

	/// <summary>Lines per owner, most first ("Watchlist · US 40, Chart 2"), above the list.</summary>
	[ObservableProperty]
	public partial string MarketDataOwnersText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial bool HasMarketDataStreams { get; set; }

	public LayoutDto GetLayout() => _settingsManager.GetLayout();

	/// <summary>Saves every window's layout, with the main window's bounds, state and screen.</summary>
	public void SaveLayout(WindowPlacementDto placement)
	{
		var layout = _settingsManager.GetLayout();
		WindowPlacementHelpers.SetMain(layout, placement);
		CurrentPage.FillLayout(layout);
		_settingsManager.SaveLayout(layout);
	}

	[RelayCommand]
	public void RefreshAll() => CurrentPage.RefreshAll();

	/// <summary>Menu → New window (Ctrl+Shift+N): an empty workspace in a window of its own.</summary>
	[RelayCommand]
	public void NewWindow() => Windows.Open();

	public void ShowTool(WorkspaceTool tool) => CurrentPage.ShowTool(tool);

	/// <summary>Menu → Widgets: a new instance of the widget, focused.</summary>
	public void SpawnTool(WorkspaceTool tool) => CurrentPage.SpawnTool(tool);

	/// <summary>Ctrl+Alt+key: shows the widget (an open instance, else a new one) and focuses it.</summary>
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
			$"Market data: {_settingsManager.GetAppSettings().MarketDataType} · streams {_marketDataManager.ActiveLines}/{MarketDataConstants.MaxLines}";

	// Any thread (the manager raises it where the line was taken or released)
	private void OnStreamsChanged()
	{
		if (Interlocked.Exchange(ref _streamsRefreshPosted, 1) == 0)
			Dispatcher.UIThread.Post(RefreshStreams, DispatcherPriority.Background);
	}

	// UI thread: the list follows the manager's snapshot, keeping rows that are still open; a
	// shared line whose owners changed comes as a new entry and replaces its row in place
	private void RefreshStreams()
	{
		Volatile.Write(ref _streamsRefreshPosted, 0);
		var streams = _marketDataManager.GetActiveStreams();
		var open = streams.ToDictionary(x => x.Id);
		for (var i = MarketDataStreams.Count - 1; i >= 0; i--)
			if (!open.TryGetValue(MarketDataStreams[i].Stream.Id, out var stream))
				MarketDataStreams.RemoveAt(i);
			else if (!ReferenceEquals(stream, MarketDataStreams[i].Stream))
				MarketDataStreams[i] = new MarketDataStreamRowViewModel(
					stream,
					_displayTime.Format(stream.OpenedAt)
				);
		var shown = MarketDataStreams.Select(x => x.Stream.Id).ToHashSet();
		foreach (var stream in streams)
			if (!shown.Contains(stream.Id))
				MarketDataStreams.Add(
					new MarketDataStreamRowViewModel(stream, _displayTime.Format(stream.OpenedAt))
				);
		MarketDataOwnersText = MarketDataStreamHelpers.GetOwnerSummary(streams);
		HasMarketDataStreams = streams.Count > 0;
		UpdateStreamAges();
		UpdateStatusBar();
	}

	private void UpdateStreamAges()
	{
		var now = _displayTime.Now;
		foreach (var row in MarketDataStreams)
			row.AgeText = MarketDataStreamHelpers.FormatAge(now - row.Stream.OpenedAt);
	}
}
