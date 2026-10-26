using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using LT.App.Controls;

namespace LT.App.Widgets.Settings;

public partial class SettingsWindow : AppWindow
{
	public SettingsWindow()
	{
		InitializeComponent();
		// Typing starts a search right away; Ctrl+F comes back to it
		Opened += (_, _) => SearchBox.Focus();
		AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
		Closed += (_, _) => (DataContext as IDisposable)?.Dispose();
	}

	private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
	{
		var ctrl =
			e.KeyModifiers.HasFlag(KeyModifiers.Control)
			|| e.KeyModifiers.HasFlag(KeyModifiers.Meta);
		if (e.Key != Key.F || !ctrl)
			return;
		SearchBox.Focus();
		SearchBox.SelectAll();
		e.Handled = true;
	}

	private void OnSaveClick(object? sender, RoutedEventArgs e)
	{
		if (DataContext is SettingsViewModel vm && vm.Save())
			Close(true);
	}

	private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

	// A setup value's Copy button
	private async void OnCopyClick(object? sender, RoutedEventArgs e)
	{
		if (
			(sender as Control)?.DataContext is GuideValueViewModel value
			&& Clipboard is { } clipboard
		)
			await clipboard.SetTextAsync(value.Value);
	}

	// A setup step's link: the default browser, through Avalonia's launcher (every OS)
	private async void OnOpenLinkClick(object? sender, RoutedEventArgs e)
	{
		if ((sender as Control)?.DataContext is not GuideStepViewModel { Url: { } url })
			return;
		try
		{
			if (!await Launcher.LaunchUriAsync(new Uri(url)) && DataContext is SettingsViewModel vm)
				vm.ErrorMessage = $"Couldn't open the browser; open {url} yourself.";
		}
		catch (Exception ex) when (DataContext is SettingsViewModel vm)
		{
			vm.ErrorMessage = $"Couldn't open the browser ({ex.Message}); open {url} yourself.";
		}
	}
}
