using CommunityToolkit.Mvvm.Input;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.App.Workspace;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Shell;

/// <summary>
/// An extra workspace window (menu → New window): a workspace of its own, filled from its own
/// menu. Created by <see cref="WorkspaceWindows"/>, not by DI (it carries its saved layout).
/// </summary>
public partial class WorkspaceWindowViewModel : ViewModelBase
{
	private readonly WorkspaceWindows _windows;

	public WorkspaceWindowViewModel(
		WorkspaceWindows windows,
		WorkspaceWindowDto layout,
		WorkspaceViewModel workspace
	)
	{
		_windows = windows;
		Workspace = workspace;
		Id = layout.Id;
		Name = layout.Name;
		Placement = WindowPlacementHelpers.GetWindow(layout);
	}

	public string Id { get; }
	public string Name { get; }
	public string Title => $"LimitlessTrading · {Name}";

	public WorkspaceViewModel Workspace { get; }

	/// <summary>Cards or flush: the window carries the flush class the styles key on.</summary>
	public AppearanceStore? Appearance => Workspace.Appearance;

	/// <summary>
	/// Normal bounds, state and screen; written by the window before the layout is saved.
	/// </summary>
	public WindowPlacementDto Placement { get; set; }

	[RelayCommand]
	public void NewWindow() => _windows.Open();

	[RelayCommand]
	public void RefreshAll() => _windows.RefreshAll();

	public void ShowTool(WorkspaceTool tool) => Workspace.Show(tool);

	public void FocusTool(WorkspaceTool tool) => Workspace.Focus(tool);

	public void ToggleTool(WorkspaceTool tool) => Workspace.Toggle(tool);

	public void CycleView(int step) => Workspace.CycleView(step);

	public void ToggleFullscreen() => Workspace.ToggleFullscreen();

	public WorkspaceWindowDto GetLayout()
	{
		var layout = new WorkspaceWindowDto { Id = Id, Name = Name };
		WindowPlacementHelpers.SetWindow(layout, Placement);
		Workspace.FillLayout(layout);
		return layout;
	}
}
