using Avalonia.Controls;
using Avalonia.Input;
using LT.App.Workspace;
using LT.Contracts.Enums;

namespace LT.App.Widgets.Account;

public class AccountWidget : IWidgetDefinition
{
	public WorkspaceTool Tool => WorkspaceTool.Account;

	public WidgetCategory Category => WidgetCategory.Ibkr;

	public string Title => "Account";

	public string Icon =>
		"M12,3A4.5,4.5 0 1,1 12,12A4.5,4.5 0 1,1 12,3Z M3,21C3,16.5 7,14 12,14C17,14 21,16.5 21,21Z";

	public Key? FocusKey => Key.A;

	public Type ViewModelType => typeof(AccountViewModel);

	public Control CreateView() => new AccountView();

	public Control? CreateTitle() => null;
}
