using Avalonia.Controls;
using Avalonia.Input;
using LT.App.Workspace;
using LT.Contracts.Enums;

namespace LT.App.Widgets.Positions;

public class PositionsWidget : IWidgetDefinition
{
	public WorkspaceTool Tool => WorkspaceTool.Positions;

	public string Title => "Positions";

	public string Icon =>
		"M12,1L22,6L12,11L2,6Z M2,10L12,15L22,10V12L12,17L2,12Z M2,15L12,20L22,15V17L12,22L2,17Z";

	public Key? FocusKey => Key.P;

	public Type ViewModelType => typeof(PositionsViewModel);

	public Control CreateView() => new PositionsView();

	public Control? CreateTitle() => null;
}
