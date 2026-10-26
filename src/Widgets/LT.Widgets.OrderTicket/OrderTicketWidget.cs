using Avalonia.Controls;
using Avalonia.Input;
using LT.App.Workspace;
using LT.Contracts.Enums;

namespace LT.App.Widgets.OrderTicket;

public class OrderTicketWidget : IWidgetDefinition
{
	public WorkspaceTool Tool => WorkspaceTool.OrderTicket;

	public WidgetCategory Category => WidgetCategory.Ibkr;

	public string Title => "Order ticket";

	/// <summary>A price tag: buy / sell.</summary>
	public string Icon =>
		"M2,12L12,2H21C21.6,2 22,2.4 22,3V12L12,22Z M17,5A2,2 0 1,1 17,9A2,2 0 1,1 17,5Z";

	public Key? FocusKey => Key.B;

	public Type ViewModelType => typeof(OrderTicketViewModel);

	public Control CreateView() => new OrderTicketView();

	public Control? CreateTitle() => WidgetTitles.Bound(nameof(OrderTicketViewModel.Title));
}
