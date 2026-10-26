using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Helpers;
using LT.App.ViewModels;
using LT.Contracts.Dtos.Orders;

namespace LT.App.Widgets.OrderTicket;

/// <summary>An order sent from the ticket in this session, with its latest status.</summary>
public partial class TicketOrderViewModel(int orderId, string description) : ViewModelBase
{
	public int OrderId { get; } = orderId;
	public string Description { get; } = $"#{orderId} {description}";

	[ObservableProperty]
	public partial string Status { get; set; } = "Sent";

	[ObservableProperty]
	public partial string? FillText { get; set; }

	[ObservableProperty]
	public partial bool IsDone { get; set; }

	[ObservableProperty]
	public partial string? Error { get; set; }

	public void Apply(OrderStatusDto status)
	{
		Status = status.Status;
		IsDone = status.IsDone;
		FillText =
			status.Filled > 0
				? $"{FormatHelpers.Quantity(status.Filled)} filled @ {FormatHelpers.Price(status.AverageFillPrice)}"
				: null;
	}

	public void Fail(string error)
	{
		Status = "Rejected";
		IsDone = true;
		Error = error;
	}
}
