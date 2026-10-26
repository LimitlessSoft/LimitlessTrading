using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LT.App.Widgets.Watchlist;
using LT.App.Workspace;
using LT.Contracts.Constants;
using LT.Contracts.Enums;

namespace LT.App.Shell;

/// <summary>
/// An extra workspace window: its own workspace (center views, docks, floating widgets) and a
/// menu that spawns widgets into it. Not owned by the main window, so it can sit on another
/// screen. Closing it discards it; closing the main window keeps it for the next start.
/// </summary>
public partial class WorkspaceWindow : Window, IFullscreenHost
{
	private readonly WorkspaceWindows? _windows;

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
	}

	private WorkspaceWindowViewModel? ViewModel => DataContext as WorkspaceWindowViewModel;

	/// <summary>
	/// Opens the window of <paramref name="viewModel"/> at its saved bounds; a new one opens a
	/// little down and right of <paramref name="near"/>.
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
			Width = viewModel.Width,
			Height = viewModel.Height,
			WindowStartupLocation = WindowStartupLocation.Manual,
		};
		var offset = (int)(WorkspaceConstants.FloatingCascadeOffset * near.RenderScaling);
		window.Position = viewModel is { X: { } x, Y: { } y }
			? new PixelPoint((int)x, (int)y)
			: near.Position + new PixelPoint(offset, offset);
		if (viewModel.IsMaximized)
			window.WindowState = WindowState.Maximized;
		window.Show();
		return window;
	}

	public void ShowFullscreen(Control? frame)
	{
		FullscreenLayer.Child = frame;
		FullscreenLayer.IsVisible = frame != null;
	}

	/// <summary>Copies the live layout (panel sizes, floating and window bounds) into the view model.</summary>
	public void StoreLayout()
	{
		Workspace.StoreLayout();
		if (ViewModel is not { } viewModel)
			return;
		viewModel.IsMaximized = WindowState == WindowState.Maximized;
		if (WindowState != WindowState.Normal)
			return;
		viewModel.X = Position.X;
		viewModel.Y = Position.Y;
		viewModel.Width = ClientSize.Width;
		viewModel.Height = ClientSize.Height;
	}

	private void OnClosing(object? sender, WindowClosingEventArgs e)
	{
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

	private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
