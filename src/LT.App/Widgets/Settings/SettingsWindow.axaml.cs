using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace LT.App.Widgets.Settings;

public partial class SettingsWindow : Window
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
}
