using Avalonia.Controls;
using LT.Contracts.Dtos.Orders;
using Microsoft.Extensions.DependencyInjection;

namespace LT.App.Widgets.OrderTicket;

/// <summary>
/// An order ticket in a popup, opened through <c>OrderTicketNavigator</c> with a draft. It is the
/// widget's own view and view model, so it reviews and sends exactly like the Order ticket.
/// </summary>
public partial class OrderTicketWindow : Window
{
	public OrderTicketWindow() => InitializeComponent();

	public static Task ShowAsync(Window owner, OrderDraftDto draft)
	{
		var viewModel = Program.Services.GetRequiredService<OrderTicketViewModel>();
		viewModel.Prefill(draft);
		var window = new OrderTicketWindow { DataContext = viewModel };
		// Its quote line and order status streams end with the window
		window.Closed += (_, _) => viewModel.Dispose();
		return window.ShowDialog(owner);
	}
}
