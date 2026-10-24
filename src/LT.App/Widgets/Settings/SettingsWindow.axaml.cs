using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LT.App.Widgets.Settings;

public partial class SettingsWindow : Window
{
	public SettingsWindow() => InitializeComponent();

	private void OnSaveClick(object? sender, RoutedEventArgs e)
	{
		if (DataContext is SettingsViewModel vm && vm.Save())
			Close(true);
	}

	private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
