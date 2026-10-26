using Avalonia.Controls;
using Avalonia.Input;
using LT.App.Workspace;
using LT.Contracts.Enums;

namespace LT.App.Widgets.Orders;

public class OrdersWidget : IWidgetDefinition
{
	public WorkspaceTool Tool => WorkspaceTool.Orders;

	public string Title => "Orders";

	public string Icon => "M5,2H15L20,7V22H5Z M8,10V12H17V10Z M8,14V16H17V14Z M8,18V20H14V18Z";

	public Key? FocusKey => Key.O;

	public Type ViewModelType => typeof(OrdersViewModel);

	public Control CreateView() => new OrdersView();

	public Control? CreateTitle() => null;
}
