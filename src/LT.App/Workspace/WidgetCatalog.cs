using Avalonia.Input;
using LT.App.Widgets.Account;
using LT.App.Widgets.AccountPerformance;
using LT.App.Widgets.Chart;
using LT.App.Widgets.DayTradePerformance;
using LT.App.Widgets.Depth;
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
		new AccountPerformanceWidget(),
		new DayTradePerformanceWidget(),
		new DepthWidget(),
	];

	public static IWidgetDefinition Get(WorkspaceTool tool) =>
		Definitions.FirstOrDefault(x => x.Tool == tool)
		?? throw new ArgumentOutOfRangeException(nameof(tool), tool, null);

	public static string GetTitle(WorkspaceTool tool) => Get(tool).Title;

	public static string GetIcon(WorkspaceTool tool) => Get(tool).Icon;

	/// <summary>
	/// Ctrl+Alt+key that shows and focuses an open instance of the widget, spawning one when none
	/// is open (null for charts, whose Ctrl+7 always opens a new one). Mnemonic; avoids T, L, D and
	/// Delete, which desktops often reserve.
	/// </summary>
	public static Key? GetFocusKey(WorkspaceTool tool) => Get(tool).FocusKey;

	public static WorkspaceTool? FromFocusKey(Key key) =>
		Definitions.FirstOrDefault(x => x.FocusKey == key)?.Tool;

	/// <summary>
	/// The widget's shortcut for its dock icon's tooltip: its Ctrl+Alt+key when it has one,
	/// else its Ctrl+1…9 hotkey (the chart's "Ctrl+7"); null when it has neither.
	/// </summary>
	public static string? GetShortcut(WorkspaceTool tool)
	{
		if (GetFocusKey(tool) is { } key)
			return $"Ctrl+Alt+{key}";
		var index = (int)tool;
		return index < 9 ? $"Ctrl+{index + 1}" : null;
	}
}
