using Avalonia.Controls;
using Avalonia.Input;
using LT.App.Controls;
using LT.App.Workspace;
using LT.Contracts.Enums;

namespace LT.App.Widgets.Overview;

public class OverviewWidget : IWidgetDefinition
{
	public WorkspaceTool Tool => WorkspaceTool.Overview;

	public WidgetCategory Category => WidgetCategory.Ibkr;

	public string Title => "Overview";

	// A page: a heading line and three lines of text
	public string Icon =>
		"M4,3H20V21H4Z M6,5V19H18V5Z M8,7H16V9H8Z M8,11H16V12.5H8Z M8,14H14V15.5H8Z";

	// Ctrl+Alt+I, for info
	public Key? FocusKey => Key.I;

	public Type ViewModelType => typeof(OverviewViewModel);

	public Control CreateView() => new OverviewView();

	// Clicking away from the ticker box opens what was typed
	public Control? CreateTitle() => new TickerTitle { CommitOnLostFocus = true };
}
