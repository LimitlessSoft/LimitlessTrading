using Avalonia.Controls;
using Avalonia.Input;
using LT.Contracts.Enums;

namespace LT.App.Workspace;

/// <summary>
/// Menu → Widgets, the same in every window: one item per widget with its shortcut, then
/// "Fullscreen widget". The window handles <see cref="ToolClicked"/> and
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
			var item = new MenuItem
			{
				Header = WidgetCatalog.GetTitle(value),
				InputGesture = GetGesture(value),
			};
			item.Click += (_, _) => ToolClicked?.Invoke(value);
			Items.Add(item);
		}
		Items.Add(new Separator());
		var fullscreen = new MenuItem
		{
			Header = "Fullscreen widget",
			InputGesture = new KeyGesture(Key.F, KeyModifiers.Control | KeyModifiers.Shift),
		};
		fullscreen.Click += (_, _) => FullscreenClicked?.Invoke();
		Items.Add(fullscreen);
	}

	protected override Type StyleKeyOverride => typeof(MenuItem);

	public event Action<WorkspaceTool>? ToolClicked;
	public event Action? FullscreenClicked;

	// Ctrl+Alt+key for single-instance widgets, else its Ctrl+1…9 hotkey (the chart's Ctrl+7)
	private static KeyGesture? GetGesture(WorkspaceTool tool)
	{
		if (WidgetCatalog.GetFocusKey(tool) is { } key)
			return new KeyGesture(key, KeyModifiers.Control | KeyModifiers.Alt);
		var index = (int)tool;
		return index < 9 ? new KeyGesture(Key.D1 + index, KeyModifiers.Control) : null;
	}
}
