using Avalonia.Controls;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Workspace;

/// <summary>
/// Menu → Widgets, the same in every window: one item per widget, each opening a new instance of
/// it (any widget can be open several times), in one section per <see cref="WidgetCategory"/>
/// under a header (Interactive Brokers, then Tools), then "Fullscreen widget". No
/// shortcut labels: they show on the dock icons' and the fullscreen button's tooltips. The window handles <see cref="ToolClicked"/> and
/// <see cref="FullscreenClicked"/> for its own workspace.
/// </summary>
public class WidgetsMenuItem : MenuItem
{
	// Menu order; null is a separator. Split into sections by category (WidgetMenuHelpers.Group)
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
		WorkspaceTool.Overview,
		WorkspaceTool.OptionChain,
		WorkspaceTool.Depth,
		WorkspaceTool.OrderTicket,
		WorkspaceTool.News,
		WorkspaceTool.Calendar,
		WorkspaceTool.DayTradePerformance,
		null,
		WorkspaceTool.Notes,
	];

	/// <summary>The menu's sections: each category's widgets in menu order, with separators.</summary>
	public static IReadOnlyList<(
		WidgetCategory Category,
		IReadOnlyList<WorkspaceTool?> Items
	)> Sections { get; } = WidgetMenuHelpers.Group(Order, WidgetCatalog.GetCategory);

	public WidgetsMenuItem()
	{
		Header = "Widgets";
		foreach (var (category, tools) in Sections)
		{
			if (Items.Count > 0)
				Items.Add(new Separator());
			// A label, not a command: disabled so it neither highlights nor clicks
			Items.Add(
				new MenuItem
				{
					Header = WidgetMenuHelpers.GetCategoryTitle(category),
					IsEnabled = false,
				}
			);
			foreach (var tool in tools)
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
