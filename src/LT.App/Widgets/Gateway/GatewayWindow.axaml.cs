using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LT.App.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace LT.App.Widgets.Gateway;

/// <summary>Closing the window doesn't stop a start in progress — the manager owns it.</summary>
public partial class GatewayWindow : AppWindow
{
	public GatewayWindow() => InitializeComponent();

	/// <summary>The log-in dialog; opened from the menu, the status bar and on startup.</summary>
	public static Task ShowAsync(Window owner) =>
		new GatewayWindow
		{
			DataContext = Program.Services.GetRequiredService<GatewayViewModel>(),
		}.ShowDialog(owner);

	private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

	private async void OnBrowseGatewayClick(object? sender, RoutedEventArgs e)
	{
		if (
			DataContext is GatewayViewModel vm
			&& await PickFolderAsync("IB Gateway folder (e.g. Jts)", vm.GatewayPath) is { } path
		)
			vm.UseGatewayFolder(path);
	}

	private async Task<string?> PickFolderAsync(string title, string current)
	{
		var start = Directory.Exists(current)
			? await StorageProvider.TryGetFolderFromPathAsync(current)
			: null;
		var folders = await StorageProvider.OpenFolderPickerAsync(
			new FolderPickerOpenOptions
			{
				Title = title,
				AllowMultiple = false,
				SuggestedStartLocation = start,
			}
		);
		return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
	}
}
