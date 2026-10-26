using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LT.App.Widgets.Gateway;
using LT.App.Widgets.Settings;
using LT.App.Workspace;
using LT.Contracts.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace LT.App.Shell;

public partial class MainWindow : Window, IFullscreenHost
{
	public MainWindow()
	{
		InitializeComponent();
		AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
		Closing += OnClosing;
		Opened += OnOpened;
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
			viewModel.Update.RestartRequested += Close;
		if (ViewModel?.GetLayout() is not { } layout)
			return;

		Width = Math.Max(MinWidth, layout.WindowWidth);
		Height = Math.Max(MinHeight, layout.WindowHeight);
		if (layout.IsMaximized)
			WindowState = WindowState.Maximized;
	}

	// A Gateway still running from an earlier session is reused; otherwise Gateway is the only way
	// in and the password isn't stored, so ask right away
	private void OnOpened(object? sender, EventArgs e) => _ = AttachOrLogInAsync();

	private async Task AttachOrLogInAsync()
	{
		if (ViewModel?.ConnectionBar is not { CanLogIn: true } connectionBar)
			return;
		if (await connectionBar.AttachRunningGatewayAsync())
			return;
		await GatewayWindow.ShowAsync(this);
	}

	private void OnClosing(object? sender, WindowClosingEventArgs e)
	{
		Page.StoreLayout();
		var maximized = WindowState == WindowState.Maximized;
		ViewModel?.SaveLayout(
			maximized ? null : ClientSize.Width,
			maximized ? null : ClientSize.Height,
			maximized
		);
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
			case Key.F5:
				ViewModel?.RefreshAll();
				break;
			case Key.OemComma when ctrl:
				_ = OpenSettingsAsync();
				break;
			case Key.D7 when ctrl && shift:
				ViewModel?.SpawnTool(WorkspaceTool.Chart);
				break;
			case >= Key.D1 and <= Key.D9 when ctrl:
				ViewModel?.ToggleTool((WorkspaceTool)(e.Key - Key.D1));
				break;
			default:
				return;
		}
		e.Handled = true;
	}

	private async Task OpenSettingsAsync() =>
		await new SettingsWindow
		{
			DataContext = Program.Services.GetRequiredService<SettingsViewModel>(),
		}.ShowDialog<bool>(this);

	private void OnSettingsClick(object? sender, RoutedEventArgs e) => _ = OpenSettingsAsync();

	private void OnGatewayClick(object? sender, RoutedEventArgs e) =>
		_ = GatewayWindow.ShowAsync(this);

	private void OnNewChartClick(object? sender, RoutedEventArgs e) =>
		ViewModel?.SpawnTool(WorkspaceTool.Chart);

	private void OnFullscreenClick(object? sender, RoutedEventArgs e) =>
		ViewModel?.ToggleFullscreen();

	private void OnToolClick(object? sender, RoutedEventArgs e)
	{
		if (
			sender is MenuItem { Tag: string tag }
			&& Enum.TryParse<WorkspaceTool>(tag, out var tool)
		)
		{
			// Same as the item's shortcut: single-instance widgets also get focus
			if (WidgetCatalog.GetFocusKey(tool) != null)
				ViewModel?.FocusTool(tool);
			else
				ViewModel?.ShowTool(tool);
		}
	}

	private void OnReleaseNotesClick(object? sender, RoutedEventArgs e)
	{
		if (ViewModel?.Update.ReleasePageUrl is { } url)
			_ = Launcher.LaunchUriAsync(new Uri(url));
	}

	private void OnExitClick(object? sender, RoutedEventArgs e) => Close();
}
