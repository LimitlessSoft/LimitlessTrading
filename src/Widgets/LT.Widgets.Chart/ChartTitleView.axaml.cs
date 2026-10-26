using Avalonia.Controls;

namespace LT.App.Widgets.Chart;

/// <summary>
/// The chart's header title: its ticker (<see cref="LT.App.Controls.TickerTitle"/>; clicking it
/// types another, leaving the box cancels) and the timeframe button beside it, which opens a menu
/// of the chart's timeframes; picking one reloads the bars at that size. Indicators and templates
/// are in the chart's right-click menu (<see cref="ChartView"/>).
/// </summary>
public partial class ChartTitleView : UserControl
{
	public ChartTitleView()
	{
		InitializeComponent();
		DataContextChanged += (_, _) => FillTimeframeMenu();
		TimeframeMenu.Opening += (_, _) => CheckCurrentTimeframe();
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
}
