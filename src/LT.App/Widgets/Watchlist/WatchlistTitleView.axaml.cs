using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace LT.App.Widgets.Watchlist;

/// <summary>
/// The watchlist's header title: the shown watchlist, opening a list of all of them. A row is
/// picked by clicking its name, renamed in place (pencil; Enter or clicking away saves, Esc
/// cancels) or deleted after an inline confirmation (✕); the last row adds a watchlist.
/// </summary>
public partial class WatchlistTitleView : UserControl
{
	public WatchlistTitleView() => InitializeComponent();

	// Switch first, then close: closing detaches the row, and with it its data
	private void OnRowPicked(object? sender, RoutedEventArgs e)
	{
		if (sender is Control { DataContext: WatchlistItemViewModel item })
			item.Owner.SelectWatchlistCommand.Execute(item);
		PickerButton.Flyout?.Hide();
	}

	// A name box opens focused with the name selected, ready to type over
	private void OnRenameBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
	{
		if (e.Property != IsVisibleProperty || sender is not TextBox { IsVisible: true } box)
			return;
		Dispatcher.UIThread.Post(
			() =>
			{
				box.Focus();
				box.SelectAll();
			},
			DispatcherPriority.Loaded
		);
	}

	private void OnRenameBoxKeyDown(object? sender, KeyEventArgs e)
	{
		if (sender is not TextBox { DataContext: WatchlistItemViewModel item })
			return;
		switch (e.Key)
		{
			case Key.Enter:
				item.Owner.CommitRenameCommand.Execute(item);
				e.Handled = true;
				break;
			case Key.Escape:
				item.Owner.CancelRenameCommand.Execute(item);
				e.Handled = true;
				break;
		}
	}

	private void OnRenameBoxLostFocus(object? sender, RoutedEventArgs e)
	{
		if (sender is TextBox { DataContext: WatchlistItemViewModel item })
			item.Owner.CommitRenameCommand.Execute(item);
	}
}
