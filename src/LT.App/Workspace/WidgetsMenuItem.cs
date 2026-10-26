using Avalonia.Controls;
using LT.Contracts.Enums;

namespace LT.App.Workspace;

/// <summary>
/// Menu → Widgets, the same in every window: one item per widget, each opening a new instance of
/// it (any widget can be open several times), then "Fullscreen widget". No
/// shortcut labels: they show on the dock icons' and the fullscreen button's tooltips. The window handles <see cref="ToolClicked"/> and
/// <see cref="FullscreenClicked"/> for its own workspace.
/// </summary>
public class WidgetsMenuItem : MenuItem
{
	// Menu order; null is a separator
	private static readonly WorkspaceTool?[] Order =
	[
		WorkspaceTool.Watchlist,
		WorkspaceTool.Positions,
		WorkspaceTool.Orders,
		WorkspaceTool.Executions,
		WorkspaceTool.Account,
		WorkspaceTool.AccountPerformance,
		WorkspaceTool.Messages,
		null,
		WorkspaceTool.Chart,
		WorkspaceTool.OptionChain,
		WorkspaceTool.Depth,
		WorkspaceTool.OrderTicket,
		WorkspaceTool.News,
		WorkspaceTool.DayTradePerformance,
	];

	public WidgetsMenuItem()
	{
		Header = "Widgets";
		foreach (var tool in Order)
		{
			if (tool is not { } value)
			{
				Items.Add(new Separator());
				continue;
			}
			var item = new MenuItem { Header = WidgetCatalog.GetTitle(value) };
			item.Click += (_, _) => ToolClicked?.Invoke(value);
			Items.Add(item);
		}
		Items.Add(new Separator());
		var fullscreen = new MenuItem { Header = "Fullscreen widget" };
		fullscreen.Click += (_, _) => FullscreenClicked?.Invoke();
		Items.Add(fullscreen);
	}

	protected override Type StyleKeyOverride => typeof(MenuItem);

	public event Action<WorkspaceTool>? ToolClicked;
	public event Action? FullscreenClicked;
}
