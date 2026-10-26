using LT.App.ViewModels;
using LT.App.Widgets.Chart;
using LT.App.Workspace;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;

namespace LT.App.Pages.TradingPage;

/// <summary>Default workspace: widget instances placed in the center, on a side or floating.</summary>
public class TradingPageViewModel : ViewModelBase
{
	private readonly WorkspaceWindows _windows;

	public TradingPageViewModel(WorkspaceWindows windows)
	{
		_windows = windows;
		_windows.MainChanged += () => OnPropertyChanged(nameof(Workspace));
	}

	/// <summary>The main window's workspace; extra windows have their own.</summary>
	public WorkspaceViewModel Workspace => _windows.Main;

	public void ShowTool(WorkspaceTool tool) => Workspace.Show(tool);

	public void FocusTool(WorkspaceTool tool) => Workspace.Focus(tool);

	public void ToggleTool(WorkspaceTool tool) => Workspace.Toggle(tool);

	public void ToggleFullscreen() => Workspace.ToggleFullscreen();

	public void CycleView(int step) => Workspace.CycleView(step);

	public void FillLayout(LayoutDto layout)
	{
		Workspace.FillLayout(layout);
		_windows.FillLayout(layout);
		// New charts start with the timeframe and period of the last chart
		if (
			Workspace.Widgets.Select(x => x.Content).OfType<ChartViewModel>().LastOrDefault() is
			{ } chart
		)
		{
			layout.ChartTimeframe = chart.SelectedTimeframe.Value;
			layout.ChartPeriod = chart.SelectedPeriod.Value;
		}
	}

	/// <summary>F5: every open widget, in every window.</summary>
	public void RefreshAll() => _windows.RefreshAll();
}
