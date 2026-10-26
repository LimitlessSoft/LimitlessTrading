using LT.Contracts.Dtos.Orders;

namespace LT.App.Stores;

/// <summary>
/// Lets a widget open an order ticket in a popup, pre-filled with a draft (Positions' "Close");
/// the main window listens to <see cref="OpenRequested"/> and opens it. Nothing is sent from
/// here: the ticket reviews and sends only on the user's "Send order". Singleton (Program.cs).
/// </summary>
public class OrderTicketNavigator
{
	/// <summary>Raised on the caller's thread with the draft to open the ticket with.</summary>
	public event Action<OrderDraftDto>? OpenRequested;

	public void Open(OrderDraftDto draft) => OpenRequested?.Invoke(draft);
}
