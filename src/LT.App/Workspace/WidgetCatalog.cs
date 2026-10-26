using Avalonia.Input;
using LT.App.Widgets.Account;
using LT.App.Widgets.Chart;
using LT.App.Widgets.Executions;
using LT.App.Widgets.Messages;
using LT.App.Widgets.News;
using LT.App.Widgets.OptionChain;
using LT.App.Widgets.Orders;
using LT.App.Widgets.OrderTicket;
using LT.App.Widgets.Positions;
using LT.App.Widgets.Watchlist;
using LT.Contracts.Enums;

namespace LT.App.Workspace;

/// <summary>Display name, icon and focus shortcut of every widget kind.</summary>
public static class WidgetCatalog
{
	/// <summary>Every widget, one per <see cref="WorkspaceTool"/>; each lives in its own project.</summary>
	public static IReadOnlyList<IWidgetDefinition> Definitions { get; } =
	[
		new WatchlistWidget(),
		new PositionsWidget(),
		new OrdersWidget(),
		new ExecutionsWidget(),
		new AccountWidget(),
		new MessagesWidget(),
		new ChartWidget(),
		new OptionChainWidget(),
		new OrderTicketWidget(),
		new NewsWidget(),
	];

	public static IWidgetDefinition Get(WorkspaceTool tool) =>
		Definitions.FirstOrDefault(x => x.Tool == tool)
		?? throw new ArgumentOutOfRangeException(nameof(tool), tool, null);

	public static string GetTitle(WorkspaceTool tool) => Get(tool).Title;

	public static string GetIcon(WorkspaceTool tool) => Get(tool).Icon;

	/// <summary>
	/// Ctrl+Alt+key that shows and focuses a single-instance widget (null for charts, which can be
	/// open several times). Mnemonic; avoids T, L, D and Delete, which desktops often reserve.
	/// </summary>
	public static Key? GetFocusKey(WorkspaceTool tool) => Get(tool).FocusKey;

	public static WorkspaceTool? FromFocusKey(Key key) =>
		Definitions.FirstOrDefault(x => x.FocusKey == key)?.Tool;

	/// <summary>"Ctrl+Alt+W", for tooltips; null when the widget has no focus shortcut.</summary>
	public static string? GetFocusGesture(WorkspaceTool tool) =>
		GetFocusKey(tool) is { } key ? $"Ctrl+Alt+{key}" : null;
}
