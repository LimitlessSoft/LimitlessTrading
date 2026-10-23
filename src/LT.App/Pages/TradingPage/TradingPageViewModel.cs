using LT.App.ViewModels;
using LT.App.Widgets.Placeholder;
using LT.App.Widgets.Positions;

namespace LT.App.Pages.TradingPage;

/// <summary>Default workspace: watchlist, chart, order ticket and the account blotter.</summary>
public class TradingPageViewModel(PositionsViewModel positions) : ViewModelBase
{
	public PositionsViewModel Positions { get; } = positions;

	public PlaceholderViewModel Watchlist { get; } =
		new("Watchlist", "Contract search and streaming L1 quotes — phase 2.");

	public PlaceholderViewModel Chart { get; } =
		new("Chart", "Historical and real-time bars — phase 3.");

	public PlaceholderViewModel OrderTicket { get; } =
		new("Order ticket", "Order entry with what-if preview — phase 4.");

	public PlaceholderViewModel Orders { get; } =
		new("Orders", "Open orders with cancel / modify — phase 4.");

	public PlaceholderViewModel Executions { get; } =
		new("Executions", "Today's fills with commissions — phase 4.");
}
