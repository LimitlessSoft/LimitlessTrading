using Avalonia.Controls;
using Avalonia.Threading;
using LT.App.Controls;

namespace LT.App.Widgets.Chart;

/// <summary>
/// The chart's header title. Clicking it swaps in a ticker box: typing lists IB's listings of that
/// symbol, an empty box (or the chevron) lists recent tickers. Enter opens the highlighted listing,
/// else the typed ticker (US listing first). Esc or leaving the box cancels.
/// </summary>
public partial class ChartTitleView : UserControl
{
	public ChartTitleView()
	{
		InitializeComponent();
		TickerBox.Suggest = (text, ct) =>
			ViewModel?.GetTickerSuggestionsAsync(text, ct)
			?? Task.FromResult(Enumerable.Empty<object>());
		TickerBox.Picked += Open;
		TickerBox.Entered += text => _ = OpenTypedAsync(text);
		TickerBox.Cancelled += EndEdit;
		TitleButton.Click += (_, _) => BeginEdit();
		RecentButton.Click += (_, _) => TickerBox.IsDropDownOpen = !TickerBox.IsDropDownOpen;
		TickerBox.LostFocus += (_, _) => Dispatcher.UIThread.Post(EndEditIfUnfocused);
	}

	private ChartViewModel? ViewModel => DataContext as ChartViewModel;

	private void BeginEdit()
	{
		var recents = ViewModel?.GetRecentSuggestions() ?? [];
		TickerBox.Reset(recents);
		TitleButton.IsVisible = false;
		Editor.IsVisible = true;
		Dispatcher.UIThread.Post(
			() =>
			{
				TickerBox.FocusBox();
				TickerBox.IsDropDownOpen = recents.Count > 0;
			},
			DispatcherPriority.Loaded
		);
	}

	private void EndEdit()
	{
		TickerBox.Reset();
		Editor.IsVisible = false;
		TitleButton.IsVisible = true;
	}

	// Focus can move to the dropdown or the chevron without leaving the editor
	private void EndEditIfUnfocused()
	{
		if (!Editor.IsVisible || TickerBox.IsDropDownOpen || RecentButton.IsFocused)
			return;
		if (!TickerBox.HasFocus)
			EndEdit();
	}

	private void Open(ContractSuggestionItem item)
	{
		EndEdit();
		ViewModel?.OpenContract(item.Contract);
	}

	private async Task OpenTypedAsync(string text)
	{
		if (ViewModel is not { } viewModel)
			return;
		EndEdit();
		await viewModel.OpenTickerAsync(text);
	}
}
