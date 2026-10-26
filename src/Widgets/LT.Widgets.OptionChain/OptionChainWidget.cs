using Avalonia.Controls;
using Avalonia.Input;
using LT.App.Controls;
using LT.App.Workspace;
using LT.Contracts.Enums;

namespace LT.App.Widgets.OptionChain;

public class OptionChainWidget : IWidgetDefinition
{
	public WorkspaceTool Tool => WorkspaceTool.OptionChain;

	public WidgetCategory Category => WidgetCategory.Ibkr;

	public string Title => "Option chain";

	public string Icon =>
		"M3,4H21V8H3Z M3,10H11V14H3Z M13,10H21V14H13Z M3,16H11V20H3Z M13,16H21V20H13Z";

	public Key? FocusKey => Key.C;

	public Type ViewModelType => typeof(OptionChainViewModel);

	public Control CreateView() => new OptionChainView();

	// Clicking away from the ticker box opens what was typed
	public Control? CreateTitle() => new TickerTitle { CommitOnLostFocus = true };
}
