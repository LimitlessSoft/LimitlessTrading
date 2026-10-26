using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LT.App.Controls;
using LT.App.Stores;
using LT.App.Widgets.Gateway;
using LT.App.Widgets.OrderTicket;
using LT.App.Widgets.Settings;
using LT.App.Workspace;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace LT.App.Shell;

public partial class MainWindow : Window, IFullscreenHost
{
	private readonly SettingsNavigator? _settingsNavigator;
	private readonly OrderTicketNavigator? _orderTicketNavigator;
	private bool _settingsOpen;

	// Set once closing means exiting the app (chosen, or an update restart): no more asking
	private bool _closeConfirmed;

	// Open extra workspace windows, by view model
	private readonly Dictionary<WorkspaceWindowViewModel, WorkspaceWindow> _workspaceWindows = [];

	public MainWindow()
	{
		InitializeComponent();
		AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
		Closing += OnClosing;
		Opened += OnOpened;
		// A widget's "Set up…" opens Settings on its page (null in the previewer)
		_settingsNavigator = Program.Services?.GetService<SettingsNavigator>();
		if (_settingsNavigator != null)
			_settingsNavigator.OpenRequested += OnSettingsRequested;
		// Positions' "Close" opens a pre-filled ticket in a popup
		_orderTicketNavigator = Program.Services?.GetService<OrderTicketNavigator>();
		if (_orderTicketNavigator != null)
			_orderTicketNavigator.OpenRequested += OnOrderTicketRequested;
		Closed += (_, _) =>
		{
			if (_settingsNavigator != null)
				_settingsNavigator.OpenRequested -= OnSettingsRequested;
			if (_orderTicketNavigator != null)
				_orderTicketNavigator.OpenRequested -= OnOrderTicketRequested;
			if (ViewModel?.Windows is { } windows)
				windows.Windows.CollectionChanged -= OnWorkspaceWindowsChanged;
		};
	}

	private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

	public void ShowFullscreen(Control? frame)
	{
		FullscreenLayer.Child = frame;
		FullscreenLayer.IsVisible = frame != null;
	}

	protected override void OnDataContextChanged(EventArgs e)
	{
		base.OnDataContextChanged(e);
		if (ViewModel is { } viewModel)
			// The new version starts once this process has exited
			viewModel.Update.RestartRequested += CloseApplication;
		if (ViewModel?.GetLayout() is not { } layout)
			return;

		Width = Math.Max(MinWidth, layout.WindowWidth);
		Height = Math.Max(MinHeight, layout.WindowHeight);
		if (layout.IsMaximized)
			WindowState = WindowState.Maximized;
	}

	// A Gateway still running from an earlier session is reused; otherwise Gateway is the only way
	// in and the password isn't stored, so ask right away. The extra windows of the last session
	// come back.
	private void OnOpened(object? sender, EventArgs e)
	{
		if (ViewModel?.Windows is { } windows)
		{
			foreach (var window in windows.Windows)
				OpenWorkspaceWindow(window);
			windows.Windows.CollectionChanged += OnWorkspaceWindowsChanged;
		}
		_ = AttachOrLogInAsync();
	}

	private void OnWorkspaceWindowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		foreach (var window in e.NewItems?.OfType<WorkspaceWindowViewModel>() ?? [])
			OpenWorkspaceWindow(window);
		// Discarded without the user closing it (its last view moved out): close it, after the
		// pass that took the frames out of it. One the user closed is already gone by then.
		foreach (var viewModel in e.OldItems?.OfType<WorkspaceWindowViewModel>() ?? [])
			if (_workspaceWindows.Remove(viewModel, out var window))
				Dispatcher.UIThread.Post(
					() =>
					{
						if (window.IsVisible)
							window.Close();
					},
					DispatcherPriority.Background
				);
	}

	private void OpenWorkspaceWindow(WorkspaceWindowViewModel viewModel)
	{
		if (ViewModel?.Windows is not { } windows || _workspaceWindows.ContainsKey(viewModel))
			return;
		var window = WorkspaceWindow.Open(windows, viewModel, this);
		_workspaceWindows[viewModel] = window;
		window.Closed += (_, _) => _workspaceWindows.Remove(viewModel);
	}

	private async Task AttachOrLogInAsync()
	{
		if (ViewModel?.ConnectionBar is not { CanLogIn: true } connectionBar)
			return;
		if (await connectionBar.AttachRunningGatewayAsync())
			return;
		await GatewayWindow.ShowAsync(this);
	}

	/// <summary>Exits the app without asking; every open window is kept for the next start.</summary>
	public void CloseApplication()
	{
		_closeConfirmed = true;
		Close();
	}

	private void OnClosing(object? sender, WindowClosingEventArgs e)
	{
		// ✕ with extra workspace windows open: close only this one, or the whole app?
		if (
			!_closeConfirmed
			&& e.CloseReason == WindowCloseReason.WindowClosing
			&& ViewModel?.Windows is { Windows.Count: > 0 }
		)
		{
			e.Cancel = true;
			_ = AskAndCloseAsync();
			return;
		}
		// The extra windows close with the app and are kept for the next start
		if (ViewModel?.Windows is { } windows)
			windows.IsExiting = true;
		foreach (var window in _workspaceWindows.Values)
			window.StoreLayout();
		Page.StoreLayout();
		var maximized = WindowState == WindowState.Maximized;
		ViewModel?.SaveLayout(
			maximized ? null : ClientSize.Width,
			maximized ? null : ClientSize.Height,
			maximized
		);
	}

	private async Task AskAndCloseAsync()
	{
		if (ViewModel?.Windows is not { } windows)
			return;
		var choice = await WorkspaceWindow.AskCloseAsync(
			this,
			WorkspaceWindowHelpers.GetCloseDetail(
				true,
				windows.Windows.FirstOrDefault()?.Name,
				windows.Windows.Count + 1
			)
		);
		switch (choice)
		{
			// The others may have closed meanwhile: then this is the last window and the app exits
			case ConfirmChoice.Confirm when windows.Windows.Count > 0:
				TakeOverNextWindow(windows);
				break;
			case ConfirmChoice.Confirm:
			case ConfirmChoice.Alternative:
				CloseApplication();
				break;
		}
	}

	/// <summary>
	/// Close this window on the main window: it can't go (the app exits with it), so it takes the
	/// place of the first extra window instead — that window's workspace, bounds and state — and
	/// that window closes; the main workspace is discarded (<see cref="WorkspaceWindows.ReplaceMain"/>).
	/// </summary>
	private void TakeOverNextWindow(WorkspaceWindows windows)
	{
		var next = windows.Windows[0];
		// Its window lets go of the workspace first, so the main window builds fresh frames for it
		if (_workspaceWindows.Remove(next, out var window))
		{
			window.HandOver();
			Dispatcher.UIThread.Post(window.Close, DispatcherPriority.Background);
		}
		windows.ReplaceMain();
		WindowState = WindowState.Normal;
		if (next is { X: { } x, Y: { } y })
			Position = new PixelPoint((int)x, (int)y);
		Width = Math.Max(MinWidth, next.Width);
		Height = Math.Max(MinHeight, next.Height);
		if (next.IsMaximized)
			WindowState = WindowState.Maximized;
	}

	private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
	{
		var ctrl =
			e.KeyModifiers.HasFlag(KeyModifiers.Control)
			|| e.KeyModifiers.HasFlag(KeyModifiers.Meta);
		var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
		var alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
		switch (e.Key)
		{
			case var key when ctrl && alt && WidgetCatalog.FromFocusKey(key) is { } tool:
				ViewModel?.FocusTool(tool);
				break;
			case Key.F when ctrl && shift:
				ViewModel?.ToggleFullscreen();
				break;
			case Key.F when ctrl:
				Page.FocusSearch();
				break;
			case Key.N when ctrl && shift:
				ViewModel?.NewWindow();
				break;
			case Key.Tab when ctrl:
				ViewModel?.CycleView(shift ? -1 : 1);
				break;
			case Key.F5:
				ViewModel?.RefreshAll();
				break;
			case Key.OemComma when ctrl:
				_ = OpenSettingsAsync();
				break;
			case >= Key.D1 and <= Key.D9 when ctrl:
				ViewModel?.ToggleTool((WorkspaceTool)(e.Key - Key.D1));
				break;
			default:
				return;
		}
		e.Handled = true;
	}

	private async Task OpenSettingsAsync(string? pageTitle = null)
	{
		if (_settingsOpen)
			return;
		_settingsOpen = true;
		try
		{
			var viewModel = Program.Services.GetRequiredService<SettingsViewModel>();
			if (pageTitle != null)
				viewModel.ShowPage(pageTitle);
			await new SettingsWindow { DataContext = viewModel }.ShowDialog<bool>(this);
		}
		finally
		{
			_settingsOpen = false;
		}
	}

	private void OnSettingsRequested(string? pageTitle) =>
		Dispatcher.UIThread.Post(() => _ = OpenSettingsAsync(pageTitle));

	private void OnOrderTicketRequested(OrderDraftDto draft) =>
		Dispatcher.UIThread.Post(() => _ = OrderTicketWindow.ShowAsync(this, draft));

	private void OnSettingsClick(object? sender, RoutedEventArgs e) => _ = OpenSettingsAsync();

	private void OnGatewayClick(object? sender, RoutedEventArgs e) =>
		_ = GatewayWindow.ShowAsync(this);

	private void OnFullscreenClicked() => ViewModel?.ToggleFullscreen();

	private void OnToolClicked(WorkspaceTool tool)
	{
		// Same as the item's shortcut: single-instance widgets also get focus
		if (WidgetCatalog.GetFocusKey(tool) != null)
			ViewModel?.FocusTool(tool);
		else
			ViewModel?.ShowTool(tool);
	}

	private void OnReleaseNotesClick(object? sender, RoutedEventArgs e)
	{
		if (ViewModel?.Update.ReleasePageUrl is { } url)
			_ = Launcher.LaunchUriAsync(new Uri(url));
	}

	private void OnExitClick(object? sender, RoutedEventArgs e) => Close();
}
