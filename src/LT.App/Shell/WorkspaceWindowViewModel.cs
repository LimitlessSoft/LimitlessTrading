using CommunityToolkit.Mvvm.Input;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.App.Workspace;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;

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
		X = layout.X;
		Y = layout.Y;
		Width = layout.Width;
		Height = layout.Height;
		IsMaximized = layout.IsMaximized;
	}

	public string Id { get; }
	public string Name { get; }
	public string Title => $"LimitlessTrading · {Name}";

	public WorkspaceViewModel Workspace { get; }

	/// <summary>Cards or flush: the window carries the flush class the styles key on.</summary>
	public AppearanceStore? Appearance => Workspace.Appearance;

	// Bounds, written by the window before the layout is saved
	public double? X { get; set; }
	public double? Y { get; set; }
	public double Width { get; set; }
	public double Height { get; set; }
	public bool IsMaximized { get; set; }

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
		var layout = new WorkspaceWindowDto
		{
			Id = Id,
			Name = Name,
			X = X,
			Y = Y,
			Width = Width,
			Height = Height,
			IsMaximized = IsMaximized,
		};
		Workspace.FillLayout(layout);
		return layout;
	}
}
