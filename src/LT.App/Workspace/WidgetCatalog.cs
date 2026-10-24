using LT.Contracts.Enums;

namespace LT.App.Workspace;

/// <summary>Display name and icon of every widget kind.</summary>
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
}
