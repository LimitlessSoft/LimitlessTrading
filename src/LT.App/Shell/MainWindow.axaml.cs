using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LT.App.Widgets.Gateway;
using LT.App.Widgets.Settings;
using LT.Contracts.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace LT.App.Shell;

public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
		Closing += OnClosing;
		Opened += OnOpened;
	}

	private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

	protected override void OnDataContextChanged(EventArgs e)
	{
		base.OnDataContextChanged(e);
		if (ViewModel?.GetLayout() is not { } layout)
			return;

		Width = Math.Max(MinWidth, layout.WindowWidth);
		Height = Math.Max(MinHeight, layout.WindowHeight);
		if (layout.IsMaximized)
			WindowState = WindowState.Maximized;
	}

	// Gateway is the only way in and the password isn't stored, so ask right away
	private void OnOpened(object? sender, EventArgs e)
	{
		if (ViewModel?.ConnectionBar.CanLogIn == true)
			_ = GatewayWindow.ShowAsync(this);
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
		switch (e.Key)
		{
			case Key.F when ctrl:
				Page.FocusSearch();
				break;
			case Key.F5:
				ViewModel?.RefreshAll();
				break;
			case Key.OemComma when ctrl:
				_ = OpenSettingsAsync();
				break;
			case Key.D7 when ctrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift):
				ViewModel?.SpawnTool(WorkspaceTool.Chart);
				break;
			case >= Key.D1 and <= Key.D8 when ctrl:
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

	private void OnFindClick(object? sender, RoutedEventArgs e) => Page.FocusSearch();

	private void OnNewChartClick(object? sender, RoutedEventArgs e) =>
		ViewModel?.SpawnTool(WorkspaceTool.Chart);

	private void OnToolClick(object? sender, RoutedEventArgs e)
	{
		if (
			sender is MenuItem { Tag: string tag }
			&& Enum.TryParse<WorkspaceTool>(tag, out var tool)
		)
			ViewModel?.ShowTool(tool);
	}

	private void OnExitClick(object? sender, RoutedEventArgs e) => Close();
}
