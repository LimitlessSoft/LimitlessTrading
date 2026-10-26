using Avalonia.Controls;
using Avalonia.Input;
using LT.App.Workspace;
using LT.Contracts.Enums;

namespace LT.App.Widgets.Messages;

public class MessagesWidget : IWidgetDefinition
{
	public WorkspaceTool Tool => WorkspaceTool.Messages;

	public string Title => "Messages";

	public string Icon => "M3,3H21V17H8L3,21Z M7,7V9H17V7Z M7,11V13H14V11Z";

	public Key? FocusKey => Key.M;

	public Type ViewModelType => typeof(MessagesViewModel);

	public Control CreateView() => new MessagesView();

	public Control? CreateTitle() => null;
}
