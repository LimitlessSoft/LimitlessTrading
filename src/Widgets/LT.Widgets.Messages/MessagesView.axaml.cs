using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace LT.App.Widgets.Messages;

public partial class MessagesView : UserControl
{
	public MessagesView()
	{
		InitializeComponent();
		// Tunnel: ahead of the grid's own handling, so a right click selects the row under it and
		// Ctrl+C copies our readable lines (the grid's own copy is off)
		Grid.AddHandler(PointerPressedEvent, OnGridPointerPressed, RoutingStrategies.Tunnel);
		Grid.AddHandler(KeyDownEvent, OnGridKeyDown, RoutingStrategies.Tunnel);
	}

	// A right click on a row outside the selection selects just that row, so Copy takes it
	private void OnGridPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (!e.GetCurrentPoint(Grid).Properties.IsRightButtonPressed)
			return;
		if (
			(e.Source as Visual)?.FindAncestorOfType<DataGridRow>(includeSelf: true)
				is { DataContext: MessageRowViewModel row }
			&& !Grid.SelectedItems.Contains(row)
		)
			Grid.SelectedItem = row;
	}

	private async void OnGridKeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key != Key.C || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
			return;
		e.Handled = true;
		await CopySelectedAsync();
	}

	private void OnMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e) =>
		CopyItem.IsEnabled = Grid.SelectedItems.Count > 0;

	private async void OnCopyClick(object? sender, RoutedEventArgs e) => await CopySelectedAsync();

	private async Task CopySelectedAsync()
	{
		if (
			DataContext is MessagesViewModel vm
			&& vm.GetCopyText(Grid.SelectedItems.OfType<MessageRowViewModel>()) is { } text
			&& TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard
		)
			await clipboard.SetTextAsync(text);
	}
}
