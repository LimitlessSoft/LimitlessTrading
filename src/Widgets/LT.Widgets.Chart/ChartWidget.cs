using Avalonia.Controls;
using Avalonia.Input;
using LT.App.Workspace;
using LT.Contracts.Enums;

namespace LT.App.Widgets.Chart;

public class ChartWidget : IWidgetDefinition
{
	public WorkspaceTool Tool => WorkspaceTool.Chart;

	public WidgetCategory Category => WidgetCategory.Ibkr;

	public string Title => "Chart";

	public string Icon =>
		"M3,3H5V19H21V21H3Z M7,9H9V17H7Z M11,5H13V17H11Z M15,11H17V17H15Z M19,7H21V17H19Z";

	public Key? FocusKey => null;

	public Type ViewModelType => typeof(ChartViewModel);

	public Control CreateView() => new ChartView();

	public Control? CreateTitle() => new ChartTitleView();
}
