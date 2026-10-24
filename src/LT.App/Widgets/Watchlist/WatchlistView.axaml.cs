using Avalonia.Controls;
using Avalonia.Input;

namespace LT.App.Widgets.Watchlist;

public partial class WatchlistView : UserControl
{
	public WatchlistView()
	{
		InitializeComponent();
		SearchBox.Suggest = (text, ct) =>
			ViewModel?.GetSuggestionsAsync(text, ct) ?? Task.FromResult(Enumerable.Empty<object>());
		SearchBox.Picked += item =>
		{
			SearchBox.Reset();
			ViewModel?.AddContract(item.Contract);
		};
		SearchBox.Entered += text =>
		{
			SearchBox.Reset();
			_ = ViewModel?.AddTickerAsync(text);
		};
		SearchBox.Cancelled += () => SearchBox.Reset();
	}

	public void FocusSearch() => SearchBox.FocusBox();

	private WatchlistViewModel? ViewModel => DataContext as WatchlistViewModel;

	private void OnGridKeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key != Key.Delete)
			return;
		ViewModel?.RemoveCommand.Execute(null);
		e.Handled = true;
	}
}
