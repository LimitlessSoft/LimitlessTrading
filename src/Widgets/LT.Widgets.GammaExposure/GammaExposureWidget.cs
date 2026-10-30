using Avalonia.Controls;
using Avalonia.Input;
using LT.App.Controls;
using LT.App.Workspace;
using LT.Contracts.Enums;

namespace LT.App.Widgets.GammaExposure;

public class GammaExposureWidget : IWidgetDefinition
{
	public WorkspaceTool Tool => WorkspaceTool.GammaExposure;

	public WidgetCategory Category => WidgetCategory.Ibkr;

	public string Title => "Gamma exposure";

	// Bars above and below a zero line, with a price line through them
	public string Icon =>
		"M3,11H21V13H3Z M5,5H8V11H5Z M10,8H13V11H10Z M15,13H18V19H15Z M10,13H13V16H10Z";

	public Key? FocusKey => Key.G;

	public Type ViewModelType => typeof(GammaExposureViewModel);

	public Control CreateView() => new GammaExposureView();

	// Clicking away from the ticker box opens what was typed
	public Control? CreateTitle() => new TickerTitle { CommitOnLostFocus = true };
}
