using Avalonia.Controls;
using Avalonia.Input;
using LT.App.Workspace;
using LT.Contracts.Enums;

namespace LT.App.Widgets.Depth;

public class DepthWidget : IWidgetDefinition
{
	public WorkspaceTool Tool => WorkspaceTool.Depth;

	public string Title => "Depth";

	// A ladder: ask bars above, bid bars below, a gap for the spread
	public string Icon =>
		"M10,3H20V5H10Z M6,6H20V8H6Z M12,9H20V11H12Z "
		+ "M4,13H14V15H4Z M4,16H18V18H4Z M4,19H10V21H4Z";

	public Key? FocusKey => Key.K;

	public Type ViewModelType => typeof(DepthViewModel);

	public Control CreateView() => new DepthView();

	public Control? CreateTitle() => WidgetTitles.Bound(nameof(DepthViewModel.Title));
}
