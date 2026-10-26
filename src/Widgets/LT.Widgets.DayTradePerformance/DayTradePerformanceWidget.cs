using Avalonia.Controls;
using Avalonia.Input;
using LT.App.Workspace;
using LT.Contracts.Enums;

namespace LT.App.Widgets.DayTradePerformance;

public class DayTradePerformanceWidget : IWidgetDefinition
{
	public WorkspaceTool Tool => WorkspaceTool.DayTradePerformance;

	public string Title => "Day trade performance";

	public string Icon =>
		"M3,3H5V19H21V21H3Z M7,15L11,10L14,13L19.6,6.6L21.1,7.9L14.1,15.9L11.1,12.9L8.6,16.2Z";

	// J for journal; T and D are often taken by desktops, R is Account performance
	public Key? FocusKey => Key.J;

	public Type ViewModelType => typeof(DayTradePerformanceViewModel);

	public Control CreateView() => new DayTradePerformanceView();

	public Control? CreateTitle() => null;
}
