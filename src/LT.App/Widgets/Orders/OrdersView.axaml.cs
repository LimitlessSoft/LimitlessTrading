using Avalonia.Controls;
using Avalonia.Interactivity;
using LT.Contracts.Dtos.Orders;

namespace LT.App.Widgets.Orders;

public partial class OrdersView : UserControl
{
	public OrdersView() => InitializeComponent();

	// A Command bound through $parent[DataGrid] fails inside the grid's cells, and Avalonia then
	// disables the button for good; the row's order comes from the button's DataContext instead
	private void OnCancelClick(object? sender, RoutedEventArgs e)
	{
		if (
			sender is Control { DataContext: OpenOrderDto order }
			&& DataContext is OrdersViewModel viewModel
		)
			viewModel.CancelOrderCommand.Execute(order);
	}
}
