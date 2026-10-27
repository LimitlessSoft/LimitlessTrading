using Avalonia.Controls;
using Avalonia.Input;
using LT.App.Workspace;
using LT.Contracts.Enums;

namespace LT.App.Widgets.Calendar;

public class CalendarWidget : IWidgetDefinition
{
	public WorkspaceTool Tool => WorkspaceTool.Calendar;

	public WidgetCategory Category => WidgetCategory.Ibkr;

	public string Title => "Calendar";

	// A calendar page: binding rings on top, a grid of days
	public string Icon =>
		"M7,2H9V4H15V2H17V4H19A2,2 0 0,1 21,6V20A2,2 0 0,1 19,22H5A2,2 0 0,1 3,20V6A2,2 0 0,1 5,4H7Z "
		+ "M5,9V20H19V9Z M7,11H10V14H7Z M14,11H17V14H14Z M7,16H10V19H7Z";

	// "eVents": C, E and D are taken or reserved
	public Key? FocusKey => Key.V;

	public Type ViewModelType => typeof(CalendarViewModel);

	public Control CreateView() => new CalendarView();

	public Control? CreateTitle() => null;
}
