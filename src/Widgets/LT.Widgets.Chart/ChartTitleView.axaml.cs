using Avalonia.Controls;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Chart;

/// <summary>
/// The chart's header title: its ticker (<see cref="LT.App.Controls.TickerTitle"/>; clicking it
/// types another, leaving the box cancels), the timeframe button beside it, which opens a menu
/// of the chart's timeframes (picking one reloads the bars at that size), and the period button,
/// whose menu has Auto, the rolling periods (1 day … 5 years back from now) and the calendar
/// periods (today, this week, this month, this year), each group set apart by a separator.
/// On intraday charts an RTH / ETH button after it switches the chart between regular hours only
/// and pre- and post-market bars.
/// Indicators and templates are in the chart's right-click menu (<see cref="ChartView"/>).
/// </summary>
public partial class ChartTitleView : UserControl
{
	public ChartTitleView()
	{
		InitializeComponent();
		DataContextChanged += (_, _) =>
		{
			FillTimeframeMenu();
			FillPeriodMenu();
		};
		TimeframeMenu.Opening += (_, _) => CheckCurrentTimeframe();
		PeriodMenu.Opening += (_, _) => CheckCurrentPeriod();
	}

	private ChartViewModel? ViewModel => DataContext as ChartViewModel;

	private MenuFlyout TimeframeMenu => (MenuFlyout)TimeframeButton.Flyout!;

	private MenuFlyout PeriodMenu => (MenuFlyout)PeriodButton.Flyout!;

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

	// Auto | rolling periods | calendar periods
	private void FillPeriodMenu()
	{
		PeriodMenu.Items.Clear();
		if (ViewModel is not { } viewModel)
			return;
		IReadOnlyList<ChartPeriod>[] groups =
		[
			[ChartPeriod.Auto],
			ChartPeriodHelpers.RollingPeriods,
			ChartPeriodHelpers.CalendarPeriods,
		];
		foreach (var group in groups)
		{
			if (PeriodMenu.Items.Count > 0)
				PeriodMenu.Items.Add(new Separator());
			foreach (var option in viewModel.Periods.Where(x => group.Contains(x.Value)))
			{
				var item = new MenuItem
				{
					Header = option.ToString(),
					Tag = option,
					ToggleType = MenuItemToggleType.Radio,
				};
				item.Click += (_, _) => viewModel.SetPeriod(option.Value);
				PeriodMenu.Items.Add(item);
			}
		}
	}

	// The check mark follows the chart's current timeframe
	private void CheckCurrentTimeframe()
	{
		foreach (var item in TimeframeMenu.Items.OfType<MenuItem>())
			item.IsChecked = item.Tag == ViewModel?.SelectedTimeframe;
	}

	private void CheckCurrentPeriod()
	{
		foreach (var item in PeriodMenu.Items.OfType<MenuItem>())
			item.IsChecked = Equals(item.Tag, ViewModel?.SelectedPeriod);
	}
}
