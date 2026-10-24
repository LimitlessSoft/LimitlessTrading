using Avalonia.Controls;
using Avalonia.Input;

namespace LT.App.Widgets.Watchlist;

public partial class WatchlistView : UserControl
{
	public WatchlistView() => InitializeComponent();

	public void FocusSearch()
	{
		WatchlistSearchBox.Focus();
		WatchlistSearchBox.SelectAll();
	}

	private WatchlistViewModel? ViewModel => DataContext as WatchlistViewModel;

	private void OnSearchResultDoubleTapped(object? sender, TappedEventArgs e) =>
		ViewModel?.AddCommand.Execute(SearchResultsList.SelectedItem as SearchResultItem);

	private void OnSearchResultKeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key != Key.Enter)
			return;
		ViewModel?.AddCommand.Execute(SearchResultsList.SelectedItem as SearchResultItem);
		e.Handled = true;
	}

	private void OnGridKeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key != Key.Delete)
			return;
		ViewModel?.RemoveCommand.Execute(null);
		e.Handled = true;
	}
}
