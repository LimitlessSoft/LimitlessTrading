using Avalonia.Input;
using LT.Contracts.Enums;

namespace LT.App.Workspace;

/// <summary>Display name, icon and focus shortcut of every widget kind.</summary>
public static class WidgetCatalog
{
	public static string GetTitle(WorkspaceTool tool) =>
		tool switch
		{
			WorkspaceTool.OptionChain => "Option chain",
			_ => tool.ToString(),
		};

	public static string GetIcon(WorkspaceTool tool) =>
		tool switch
		{
			WorkspaceTool.Watchlist => WidgetIcons.Watchlist,
			WorkspaceTool.Positions => WidgetIcons.Positions,
			WorkspaceTool.Orders => WidgetIcons.Orders,
			WorkspaceTool.Executions => WidgetIcons.Executions,
			WorkspaceTool.Account => WidgetIcons.Account,
			WorkspaceTool.Messages => WidgetIcons.Messages,
			WorkspaceTool.Chart => WidgetIcons.Chart,
			WorkspaceTool.OptionChain => WidgetIcons.OptionChain,
			_ => throw new ArgumentOutOfRangeException(nameof(tool), tool, null),
		};

	/// <summary>
	/// Ctrl+Alt+key that shows and focuses a single-instance widget (null for charts, which can be
	/// open several times). Mnemonic; avoids T, L, D and Delete, which desktops often reserve.
	/// </summary>
	public static Key? GetFocusKey(WorkspaceTool tool) =>
		tool switch
		{
			WorkspaceTool.Watchlist => Key.W,
			WorkspaceTool.Positions => Key.P,
			WorkspaceTool.Orders => Key.O,
			WorkspaceTool.Executions => Key.E,
			WorkspaceTool.Account => Key.A,
			WorkspaceTool.Messages => Key.M,
			WorkspaceTool.OptionChain => Key.C,
			_ => null,
		};

	public static WorkspaceTool? FromFocusKey(Key key) =>
		Enum.GetValues<WorkspaceTool>()
			.Select(x => (WorkspaceTool?)x)
			.FirstOrDefault(x => GetFocusKey(x!.Value) == key);

	/// <summary>"Ctrl+Alt+W", for tooltips; null when the widget has no focus shortcut.</summary>
	public static string? GetFocusGesture(WorkspaceTool tool) =>
		GetFocusKey(tool) is { } key ? $"Ctrl+Alt+{key}" : null;
}
