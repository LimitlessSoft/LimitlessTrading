using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace LT.App.Widgets.Chart;

/// <summary>
/// The chart's header title. Clicking it swaps in a ticker box: Enter opens the typed ticker, the
/// dropdown (chevron, or opening the box) lists recent tickers. Esc or leaving the box cancels.
/// </summary>
public partial class ChartTitleView : UserControl
{
	public ChartTitleView()
	{
		InitializeComponent();
		TitleButton.Click += (_, _) => BeginEdit();
		RecentButton.Click += (_, _) => TickerBox.IsDropDownOpen = !TickerBox.IsDropDownOpen;
		TickerBox.AddHandler(KeyDownEvent, OnTickerKeyDown, RoutingStrategies.Tunnel);
		TickerBox.DropDownClosed += OnDropDownClosed;
		TickerBox.LostFocus += (_, _) => Dispatcher.UIThread.Post(EndEditIfUnfocused);
	}

	private ChartViewModel? ViewModel => DataContext as ChartViewModel;

	private void BeginEdit()
	{
		ViewModel?.RefreshRecentContracts();
		TickerBox.Text = string.Empty;
		TickerBox.SelectedItem = null;
		TitleButton.IsVisible = false;
		Editor.IsVisible = true;
		Dispatcher.UIThread.Post(
			() =>
			{
				TickerBox.Focus();
				TickerBox.IsDropDownOpen = ViewModel?.RecentContracts.Count > 0;
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
				// A recent ticker (typed or highlighted in the dropdown) opens without asking IB
				var text = TickerBox.Text?.Trim() ?? string.Empty;
				var recent = ViewModel?.RecentContracts.FirstOrDefault(x =>
					string.Equals(x.Symbol, text, StringComparison.OrdinalIgnoreCase)
				);
				if (recent != null)
					Open(recent);
				else
					_ = OpenTypedAsync(text);
				e.Handled = true;
				break;
		}
	}

	// Picking a recent ticker with the mouse opens it right away
	private void OnDropDownClosed(object? sender, EventArgs e)
	{
		if (TickerBox.SelectedItem is RecentContractItem item && Editor.IsVisible)
			Open(item);
	}

	private void Open(RecentContractItem item)
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
