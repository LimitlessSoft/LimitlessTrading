using LT.App.ViewModels;
using LT.App.Widgets.Account;
using LT.App.Widgets.Chart;
using LT.App.Widgets.Executions;
using LT.App.Widgets.Orders;
using LT.App.Widgets.Positions;
using LT.App.Workspace;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Pages.TradingPage;

/// <summary>Default workspace: widget instances placed in the center, on a side or floating.</summary>
public class TradingPageViewModel(
	WidgetViewModelFactory widgetViewModelFactory,
	ISettingsManager settingsManager
) : ViewModelBase
{
	public WorkspaceViewModel Workspace { get; } =
		new(widgetViewModelFactory.Create, settingsManager.GetLayout());

	public void ShowTool(WorkspaceTool tool) => Workspace.Show(tool);

	public void ToggleTool(WorkspaceTool tool) => Workspace.Toggle(tool);

	public void SpawnTool(WorkspaceTool tool) => Workspace.Spawn(tool);

	public void ToggleFullscreen() => Workspace.ToggleFullscreen();

	public void FillLayout(LayoutDto layout)
	{
		Workspace.FillLayout(layout);
		// New charts start with the timeframe of the last chart
		if (
			Workspace.Widgets.Select(x => x.Content).OfType<ChartViewModel>().LastOrDefault() is
			{ } chart
		)
			layout.ChartTimeframe = chart.SelectedTimeframe.Value;
	}

	public void RefreshAll()
	{
		foreach (var content in Workspace.Widgets.Where(x => x.IsOpen).Select(x => x.Content))
		{
			switch (content)
			{
				case ChartViewModel chart:
					chart.Reload();
					break;
				case AccountViewModel account:
					account.Refresh();
					break;
				case PositionsViewModel positions:
					positions.Refresh();
					break;
				case OrdersViewModel orders:
					orders.Refresh();
					break;
				case ExecutionsViewModel executions:
					executions.Refresh();
					break;
			}
		}
	}
}
