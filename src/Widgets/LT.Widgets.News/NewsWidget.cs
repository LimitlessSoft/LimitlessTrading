using Avalonia.Controls;
using Avalonia.Input;
using LT.App.Workspace;
using LT.Contracts.Enums;

namespace LT.App.Widgets.News;

public class NewsWidget : IWidgetDefinition
{
	public WorkspaceTool Tool => WorkspaceTool.News;

	public string Title => "News";

	// A folded newspaper: the page with a picture block and text lines cut out
	public string Icon =>
		"M3,4H19V18A2,2 0 0,0 21,20H5A2,2 0 0,1 3,18Z "
		+ "M5,6V18H17V6Z "
		+ "M6,7H11V11H6Z M12,7H16V8H12Z M12,9H16V10H12Z M6,12H16V13H6Z M6,14H16V15H6Z M6,16H13V17H6Z "
		+ "M19,8H21V18A1,1 0 0,1 20,19A1,1 0 0,1 19,18Z";

	public Key? FocusKey => Key.N;

	public Type ViewModelType => typeof(NewsViewModel);

	public Control CreateView() => new NewsView();

	public Control? CreateTitle() => WidgetTitles.Bound(nameof(NewsViewModel.Title));
}
