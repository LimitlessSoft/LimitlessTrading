using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LT.App.Controls;

namespace LT.App.Widgets.Chart;

/// <summary>
/// The chart's header title. Clicking it swaps in a ticker box: typing lists IB's listings of that
/// symbol, an empty box (or the chevron) lists recent tickers. Enter opens the highlighted listing,
/// else the typed ticker (US listing first). Esc or leaving the box cancels. The timeframe button
/// beside it opens a menu of the chart's timeframes; picking one reloads the bars at that size.
/// ƒx opens the indicator picker: the built-ins and the scripts, searchable (Enter adds the
/// first match), and a link to the scripts folder.
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
		DataContextChanged += (_, _) => FillTimeframeMenu();
		TimeframeMenu.Opening += (_, _) => CheckCurrentTimeframe();
		IndicatorsFlyout.Opening += (_, _) => OnIndicatorsOpening();
		IndicatorSearchBox.KeyDown += OnIndicatorSearchKeyDown;
		ScriptsFolderButton.Click += (_, _) => _ = OpenScriptsFolderAsync();
	}

	private Flyout IndicatorsFlyout => (Flyout)IndicatorsButton.Flyout!;

	private void OnIndicatorsOpening()
	{
		ViewModel?.RefreshIndicatorChoices();
		Dispatcher.UIThread.Post(
			() =>
			{
				IndicatorSearchBox.Focus();
				IndicatorSearchBox.SelectAll();
			},
			DispatcherPriority.Loaded
		);
	}

	private void OnIndicatorPicked(object? sender, RoutedEventArgs e)
	{
		if ((sender as Control)?.DataContext is IndicatorChoiceItem item)
			AddIndicator(item);
	}

	private void OnIndicatorSearchKeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key != Key.Enter || ViewModel?.IndicatorChoices is not [var first, ..])
			return;
		e.Handled = true;
		AddIndicator(first);
	}

	private void AddIndicator(IndicatorChoiceItem item)
	{
		IndicatorsFlyout.Hide();
		ViewModel?.AddIndicator(item.Id);
	}

	private async Task OpenScriptsFolderAsync()
	{
		IndicatorsFlyout.Hide();
		if (
			ViewModel is not { } viewModel
			|| TopLevel.GetTopLevel(this)?.Launcher is not { } launcher
		)
			return;
		var folder = new DirectoryInfo(viewModel.IndicatorScriptsDirectory);
		folder.Create();
		await launcher.LaunchDirectoryInfoAsync(folder);
	}

	private ChartViewModel? ViewModel => DataContext as ChartViewModel;

	private MenuFlyout TimeframeMenu => (MenuFlyout)TimeframeButton.Flyout!;

	// Filled up front: an empty MenuFlyout does not open, so filling it in Opening is too late
	private void FillTimeframeMenu()
	{
		TimeframeMenu.Items.Clear();
		if (ViewModel is not { } viewModel)
			return;
		foreach (var option in viewModel.Timeframes)
		{
			var item = new MenuItem
			{
				Header = option.ToString(),
				Tag = option,
				ToggleType = MenuItemToggleType.Radio,
			};
			item.Click += (_, _) => viewModel.SetTimeframe(option.Value);
			TimeframeMenu.Items.Add(item);
		}
	}

	// The check mark follows the chart's current timeframe
	private void CheckCurrentTimeframe()
	{
		foreach (var item in TimeframeMenu.Items.OfType<MenuItem>())
			item.IsChecked = item.Tag == ViewModel?.SelectedTimeframe;
	}

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
