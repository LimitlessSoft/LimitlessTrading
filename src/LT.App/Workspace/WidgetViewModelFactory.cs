using LT.App.Widgets.Account;
using LT.App.Widgets.Chart;
using LT.App.Widgets.Executions;
using LT.App.Widgets.Messages;
using LT.App.Widgets.OptionChain;
using LT.App.Widgets.Orders;
using LT.App.Widgets.OrderTicket;
using LT.App.Widgets.Positions;
using LT.App.Widgets.Watchlist;
using LT.Contracts.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace LT.App.Workspace;

/// <summary>
/// Creates a widget's view model from DI when an instance is spawned, so several charts each get
/// their own. Registered as a singleton in Program.cs.
/// </summary>
public class WidgetViewModelFactory(IServiceProvider services)
{
	public object Create(WorkspaceTool tool) =>
		tool switch
		{
			WorkspaceTool.Watchlist => services.GetRequiredService<WatchlistViewModel>(),
			WorkspaceTool.Positions => services.GetRequiredService<PositionsViewModel>(),
			WorkspaceTool.Orders => services.GetRequiredService<OrdersViewModel>(),
			WorkspaceTool.Executions => services.GetRequiredService<ExecutionsViewModel>(),
			WorkspaceTool.Account => services.GetRequiredService<AccountViewModel>(),
			WorkspaceTool.Messages => services.GetRequiredService<MessagesViewModel>(),
			WorkspaceTool.Chart => services.GetRequiredService<ChartViewModel>(),
			WorkspaceTool.OptionChain => services.GetRequiredService<OptionChainViewModel>(),
			WorkspaceTool.OrderTicket => services.GetRequiredService<OrderTicketViewModel>(),
			_ => throw new ArgumentOutOfRangeException(nameof(tool), tool, null),
		};
}
