using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace LT.App.Widgets.Chart;

/// <summary>
/// The chart's header title. Clicking it swaps in a ticker box: typing lists IB's listings of that
/// symbol, an empty box (or the chevron) lists recent tickers. Enter opens the highlighted listing,
/// else the typed ticker (US listing first). Esc or leaving the box cancels.
/// </summary>
public partial class ChartTitleView : UserControl
{
	// Text the dropdown's items were searched for; a highlighted item only counts for that text
	private string? _suggestedFor;

	public ChartTitleView()
	{
		InitializeComponent();
		TickerBox.AsyncPopulator = PopulateAsync;
		TitleButton.Click += (_, _) => BeginEdit();
		RecentButton.Click += (_, _) => TickerBox.IsDropDownOpen = !TickerBox.IsDropDownOpen;
		TickerBox.AddHandler(KeyDownEvent, OnTickerKeyDown, RoutingStrategies.Tunnel);
		TickerBox.DropDownClosed += OnDropDownClosed;
		TickerBox.LostFocus += (_, _) => Dispatcher.UIThread.Post(EndEditIfUnfocused);
	}

	private ChartViewModel? ViewModel => DataContext as ChartViewModel;

	private void BeginEdit()
	{
		var recents = ViewModel?.GetRecentSuggestions() ?? [];
		TickerBox.Text = string.Empty;
		TickerBox.SelectedItem = null;
		TickerBox.ItemsSource = recents;
		_suggestedFor = string.Empty;
		TitleButton.IsVisible = false;
		Editor.IsVisible = true;
		Dispatcher.UIThread.Post(
			() =>
			{
				TickerBox.Focus();
				TickerBox.IsDropDownOpen = recents.Count > 0;
			},
			DispatcherPriority.Loaded
		);
	}

	private void EndEdit()
	{
		TickerBox.IsDropDownOpen = false;
		Editor.IsVisible = false;
		TitleButton.IsVisible = true;
	}

	// Focus can move to the dropdown or the chevron without leaving the editor
	private void EndEditIfUnfocused()
	{
		if (!Editor.IsVisible || TickerBox.IsDropDownOpen || RecentButton.IsFocused)
			return;
		if (!TickerBox.IsKeyboardFocusWithin)
			EndEdit();
	}

	private void OnTickerKeyDown(object? sender, KeyEventArgs e)
	{
		switch (e.Key)
		{
			case Key.Escape:
				EndEdit();
				e.Handled = true;
				break;
			case Key.Enter:
				var text = TickerBox.Text?.Trim() ?? string.Empty;
				if (
					TickerBox.SelectedItem is ContractSuggestionItem item
					&& string.Equals(_suggestedFor, text, StringComparison.OrdinalIgnoreCase)
				)
					Open(item);
				else
					_ = OpenTypedAsync(text);
				e.Handled = true;
				break;
		}
	}

	// Picking a recent ticker with the mouse opens it right away
	private void OnDropDownClosed(object? sender, EventArgs e)
	{
		if (TickerBox.SelectedItem is ContractSuggestionItem item && Editor.IsVisible)
			Open(item);
	}

	private async Task<IEnumerable<object>> PopulateAsync(
		string? text,
		CancellationToken cancellationToken
	)
	{
		if (ViewModel is not { } viewModel)
			return [];
		var items = await viewModel.GetTickerSuggestionsAsync(text, cancellationToken);
		_suggestedFor = text?.Trim() ?? string.Empty;
		return items;
	}

	private void Open(ContractSuggestionItem item)
	{
		EndEdit();
		ViewModel?.OpenContract(item.Contract);
	}

	private async Task OpenTypedAsync(string? text)
	{
		if (string.IsNullOrWhiteSpace(text) || ViewModel is not { } viewModel)
			return;
		EndEdit();
		await viewModel.OpenTickerAsync(text);
	}
}
