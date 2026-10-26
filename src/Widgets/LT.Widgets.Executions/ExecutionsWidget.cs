using Avalonia.Controls;
using Avalonia.Input;
using LT.App.Workspace;
using LT.Contracts.Enums;

namespace LT.App.Widgets.Executions;

public class ExecutionsWidget : IWidgetDefinition
{
	public WorkspaceTool Tool => WorkspaceTool.Executions;

	public string Title => "Executions";

	public string Icon => "M13,2L4,14H11L10,22L20,9H13Z";

	public Key? FocusKey => Key.E;

	public Type ViewModelType => typeof(ExecutionsViewModel);

	public Control CreateView() => new ExecutionsView();

	public Control? CreateTitle() => null;
}
