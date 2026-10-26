using Avalonia.Controls;
using Avalonia.Input;
using LT.App.Workspace;
using LT.Contracts.Enums;

namespace LT.App.Widgets.DayTradePerformance;

public class DayTradePerformanceWidget : IWidgetDefinition
{
	public WorkspaceTool Tool => WorkspaceTool.DayTradePerformance;

	public string Title => "Day trade performance";

	// Clock (intraday), so it doesn't read as Account performance's rising line
	public string Icon =>
		"M12,2A10,10 0 1,1 12,22A10,10 0 1,1 12,2Z "
		+ "M12,4A8,8 0 1,1 12,20A8,8 0 1,1 12,4Z "
		+ "M11,6H13V11.4L16.6,13.5L15.6,15.2L11,12.6Z";

	// J for journal; T and D are often taken by desktops, R is Account performance
	public Key? FocusKey => Key.J;

	public Type ViewModelType => typeof(DayTradePerformanceViewModel);

	public Control CreateView() => new DayTradePerformanceView();

	public Control? CreateTitle() => null;
}
