using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LT.App.Controls;
using LT.App.Widgets.Watchlist;
using LT.App.Workspace;
using LT.Contracts.Constants;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Shell;

/// <summary>
/// An extra workspace window: its own workspace (center views, docks, floating widgets) and a
/// menu that spawns widgets into it. Not owned by the main window, so it can sit on another
/// screen. Closing it discards it; closing the main window keeps it for the next start.
/// </summary>
public partial class WorkspaceWindow : AppWindow, IFullscreenHost
{
	private readonly WorkspaceWindows? _windows;
	private readonly WindowPlacementTracker _placement;

	// Set once the user chose to close it (or picked Close window in its menu): no more asking
	private bool _closeConfirmed;

	// Previewer only
	public WorkspaceWindow()
		: this(null) { }

	public WorkspaceWindow(WorkspaceWindows? windows)
	{
		_windows = windows;
		InitializeComponent();
		MinWidth = WorkspaceConstants.WindowMinWidth;
		MinHeight = WorkspaceConstants.WindowMinHeight;
		AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
		Closing += OnClosing;
		_placement = new WindowPlacementTracker(this);
	}

	private WorkspaceWindowViewModel? ViewModel => DataContext as WorkspaceWindowViewModel;

	/// <summary>
	/// Opens the window of <paramref name="viewModel"/> at its saved bounds, state and screen; a new
	/// one opens a little down and right of <paramref name="near"/>.
	/// </summary>
	public static WorkspaceWindow Open(
		WorkspaceWindows windows,
		WorkspaceWindowViewModel viewModel,
		Window near
	)
	{
		var window = new WorkspaceWindow(windows)
		{
			DataContext = viewModel,
			WindowStartupLocation = WindowStartupLocation.Manual,
		};
		var offset = (int)(WorkspaceConstants.FloatingCascadeOffset * near.RenderScaling);
		window._placement.Restore(
			viewModel.Placement,
			near.Position + new PixelPoint(offset, offset)
		);
		window.Show();
		return window;
	}

	public void ShowFullscreen(Control? frame)
	{
		FullscreenLayer.Child = frame;
		FullscreenLayer.IsVisible = frame != null;
	}

	/// <summary>
	/// Copies the live layout (panel sizes, floating windows, this window's bounds, state and
	/// screen) into the view model.
	/// </summary>
	public void StoreLayout()
	{
		Workspace.StoreLayout();
		ViewModel?.Placement = _placement.Capture();
	}

	/// <summary>
	/// ✕ on a workspace window while others are open: Close this window (Enter), Close application
	/// or Cancel.
	/// </summary>
	public static Task<ConfirmChoice> AskCloseAsync(Window owner, string detail) =>
		ConfirmDialog.ShowChoiceAsync(
			owner,
			"Close window",
			"Close this window or the whole application?",
			detail,
			"Close this window",
			"Close application"
		);

	private void OnClosing(object? sender, WindowClosingEventArgs e)
	{
		// The user closing a window that is still open in the app (not one discarded or handed to
		// the main window, nor the app exiting): ask what to close
		if (
			!_closeConfirmed
			&& _windows is { IsExiting: false }
			&& e.CloseReason == WindowCloseReason.WindowClosing
			&& ViewModel is { } open
			&& _windows.Windows.Contains(open)
		)
		{
			e.Cancel = true;
			_ = AskAndCloseAsync();
			return;
		}
		StoreLayout();
		// The user closed it (✕, Alt+F4, the menu): it is gone. The app exiting keeps it.
		if (
			_windows is { IsExiting: false }
			&& e.CloseReason == WindowCloseReason.WindowClosing
			&& ViewModel is { } viewModel
		)
			_windows.Discard(viewModel);
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
				FocusSearch();
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
			case >= Key.D1 and <= Key.D9 when ctrl:
				ViewModel?.ToggleTool((WorkspaceTool)(e.Key - Key.D1));
				break;
			default:
				return;
		}
		e.Handled = true;
	}

	// Ctrl+F: the watchlist's search, bringing the watchlist into this window
	private void FocusSearch()
	{
		ViewModel?.ShowTool(WorkspaceTool.Watchlist);
		Dispatcher.UIThread.Post(
			() =>
				(Workspace.GetWidgetView(WorkspaceTool.Watchlist) as WatchlistView)?.FocusSearch(),
			DispatcherPriority.Loaded
		);
	}

	private void OnToolClicked(WorkspaceTool tool)
	{
		// Same as the item's shortcut: single-instance widgets also get focus
		if (WidgetCatalog.GetFocusKey(tool) != null)
			ViewModel?.FocusTool(tool);
		else
			ViewModel?.ShowTool(tool);
	}

	private void OnFullscreenClicked() => ViewModel?.ToggleFullscreen();

	private async Task AskAndCloseAsync()
	{
		var choice = await AskCloseAsync(
			this,
			WorkspaceWindowHelpers.GetCloseDetail(false, null, _windows!.Windows.Count + 1)
		);
		switch (choice)
		{
			case ConfirmChoice.Confirm:
				_closeConfirmed = true;
				Close();
				break;
			case ConfirmChoice.Alternative:
				// The app exits with the main window, which keeps every window for the next start
				if (
					Application.Current?.ApplicationLifetime
					is IClassicDesktopStyleApplicationLifetime { MainWindow: MainWindow main }
				)
					main.CloseApplication();
				break;
		}
	}

	/// <summary>
	/// The main window took this window's workspace over: it lets go of it (its frames, floating
	/// windows) and closes without asking.
	/// </summary>
	public void HandOver()
	{
		StoreLayout();
		_closeConfirmed = true;
		DataContext = null;
	}

	// Menu → Close window is already the choice
	private void OnCloseClick(object? sender, RoutedEventArgs e)
	{
		_closeConfirmed = true;
		Close();
	}
}
