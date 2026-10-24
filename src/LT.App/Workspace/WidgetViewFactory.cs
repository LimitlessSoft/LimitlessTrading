using Avalonia.Controls;
using LT.App.Widgets.Account;
using LT.App.Widgets.Chart;
using LT.App.Widgets.Executions;
using LT.App.Widgets.Messages;
using LT.App.Widgets.OptionChain;
using LT.App.Widgets.Orders;
using LT.App.Widgets.Positions;
using LT.App.Widgets.Watchlist;
using LT.Contracts.Enums;

namespace LT.App.Workspace;

/// <summary>Creates the view of a widget; each view is created once and moved between hosts.</summary>
public static class WidgetViewFactory
{
	public static Control Create(WidgetItem widget)
	{
		Control view = widget.Tool switch
		{
			WorkspaceTool.Watchlist => new WatchlistView(),
			WorkspaceTool.Positions => new PositionsView(),
			WorkspaceTool.Orders => new OrdersView(),
			WorkspaceTool.Executions => new ExecutionsView(),
			WorkspaceTool.Account => new AccountView(),
			WorkspaceTool.Messages => new MessagesView(),
			WorkspaceTool.Chart => new ChartView(),
			WorkspaceTool.OptionChain => new OptionChainView(),
			_ => throw new ArgumentOutOfRangeException(nameof(widget), widget.Tool, null),
		};
		view.DataContext = widget.Content;
		return view;
	}

	/// <summary>A header title of the widget's own, or null for the plain title text.</summary>
	public static Control? CreateTitle(WidgetItem widget) =>
		widget.Tool switch
		{
			WorkspaceTool.Chart => new ChartTitleView { DataContext = widget.Content },
			_ => null,
		};
}
