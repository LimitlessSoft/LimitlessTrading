using Avalonia.Controls;
using Avalonia.Input;
using LT.App.Workspace;
using LT.Contracts.Enums;

namespace LT.App.Widgets.Notes;

public class NotesWidget : IWidgetDefinition
{
	public WorkspaceTool Tool => WorkspaceTool.Notes;

	public WidgetCategory Category => WidgetCategory.Tools;

	public string Title => "Notes";

	// A page with a folded corner and three lines
	public string Icon =>
		"M5,2H15L20,7V22H5Z M14,3V8H19Z M8,11V13H17V11Z M8,15V17H17V15Z M8,19V20H14V19Z";

	// S for scratch pad (N is News)
	public Key? FocusKey => Key.S;

	public Type ViewModelType => typeof(NotesViewModel);

	public Control CreateView() => new NotesView();

	public Control? CreateTitle() => null;
}
