using Avalonia.Controls;
using Avalonia.Input;
using LT.App.Workspace;
using LT.Contracts.Enums;

namespace LT.App.Widgets.AccountPerformance;

public class AccountPerformanceWidget : IWidgetDefinition
{
	public WorkspaceTool Tool => WorkspaceTool.AccountPerformance;

	public WidgetCategory Category => WidgetCategory.Ibkr;

	public string Title => "Account performance";

	public string Icon =>
		"M3,3H5V19H21V21H3Z M6,15L11,10L14,13L18.6,8.4L17,7H22V12L20.4,10.4L14,16.8L11,13.8L7.4,17.4Z";

	public Key? FocusKey => Key.R;

	public Type ViewModelType => typeof(AccountPerformanceViewModel);

	public Control CreateView() => new AccountPerformanceView();

	public Control? CreateTitle() => null;
}
