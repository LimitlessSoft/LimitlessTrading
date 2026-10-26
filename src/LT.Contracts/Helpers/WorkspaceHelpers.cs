using LT.Contracts.Constants;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

public static class WorkspaceHelpers
{
	/// <summary>Where a widget opens when it is spawned and has no saved placement.</summary>
	public static WidgetDock GetDefaultDock(WorkspaceTool tool) =>
		tool switch
		{
			WorkspaceTool.Watchlist
			or WorkspaceTool.Positions
			or WorkspaceTool.Orders
			or WorkspaceTool.Executions => WidgetDock.Left,
			WorkspaceTool.Account
			or WorkspaceTool.Messages
			or WorkspaceTool.OrderTicket
			or WorkspaceTool.Depth => WidgetDock.Right,
			WorkspaceTool.News => WidgetDock.Bottom,
			_ => WidgetDock.Center,
		};

	/// <summary>Widgets that can be open several times at once; every other widget is single.</summary>
	public static bool AllowsMultipleInstances(WorkspaceTool tool) => tool == WorkspaceTool.Chart;

	/// <summary>
	/// Whether a press that has moved by (<paramref name="deltaX"/>, <paramref name="deltaY"/>)
	/// DIPs is a drag rather than a click: at least <see cref="WorkspaceConstants.DragThreshold"/>
	/// along either axis.
	/// </summary>
	public static bool IsBeyondDragThreshold(double deltaX, double deltaY) =>
		Math.Abs(deltaX) >= WorkspaceConstants.DragThreshold
		|| Math.Abs(deltaY) >= WorkspaceConstants.DragThreshold;

	/// <summary>
	/// Where Esc in a side or bottom widget sends keyboard focus: the center widget focused last
	/// (<paramref name="lastCenterId"/>) while it is still in the center, else the first center
	/// widget; null when the center is empty.
	/// </summary>
	public static string? GetFocusReturnId(string? lastCenterId, IEnumerable<string> centerIds)
	{
		var ids = centerIds.ToList();
		return lastCenterId != null && ids.Contains(lastCenterId)
			? lastCenterId
			: ids.FirstOrDefault();
	}

	/// <summary>Instance id of a single-instance widget (and of the first instance of the others).</summary>
	public static string GetDefaultId(WorkspaceTool tool) => tool.ToString();

	/// <summary>
	/// <paramref name="id"/> when no widget in <paramref name="taken"/> has it, else the id with a
	/// short random suffix ("Chart-1a2b3c4d").
	/// </summary>
	public static string GetFreeId(string id, IReadOnlyCollection<string> taken)
	{
		if (!taken.Contains(id))
			return id;
		while (true)
		{
			var candidate = $"{id}-{Guid.NewGuid():N}"[..(id.Length + 9)];
			if (!taken.Contains(candidate))
				return candidate;
		}
	}

	/// <summary>
	/// First-run workspace: the chart (SPY, 5m) in the center; the watchlist on the left; account,
	/// messages, the option chain and the order ticket on the right, the first two pinned. The
	/// others (positions, orders, executions, news, performance) are opened from the menu.
	/// </summary>
	public static List<WidgetPlacementEntity> GetDefaultPlacements() =>
		[
			new()
			{
				Id = GetDefaultId(WorkspaceTool.Chart),
				Tool = WorkspaceTool.Chart,
				Dock = WidgetDock.Center,
				ChartTimeframe = ChartTimeframe.FiveMinutes,
				ChartContract = ContractHelpers.GetStarterChartContract(),
			},
			new()
			{
				Id = GetDefaultId(WorkspaceTool.Watchlist),
				Tool = WorkspaceTool.Watchlist,
				Dock = WidgetDock.Left,
				IsPinned = false,
				SideWidth = WorkspaceConstants.DefaultWatchlistWidth,
			},
			CreateRightPlacement(WorkspaceTool.Account, true),
			CreateRightPlacement(WorkspaceTool.Messages, true),
			CreateRightPlacement(
				WorkspaceTool.OptionChain,
				false,
				WorkspaceConstants.DefaultOptionChainWidth
			),
			CreateRightPlacement(WorkspaceTool.OrderTicket, false),
		];

	private static WidgetPlacementEntity CreateRightPlacement(
		WorkspaceTool tool,
		bool isPinned,
		double sideWidth = WorkspaceConstants.DefaultRightWidgetWidth
	) =>
		new()
		{
			Id = GetDefaultId(tool),
			Tool = tool,
			Dock = WidgetDock.Right,
			IsPinned = isPinned,
			SideWidth = sideWidth,
		};

	/// <summary>
	/// Cleans a saved layout: drops unknown widgets/docks, gives id-less entries (older layouts)
	/// their default id, and drops duplicate ids and second instances of single-instance widgets.
	/// </summary>
	public static List<WidgetPlacementDto> Normalize(IEnumerable<WidgetPlacementDto> placements)
	{
		var result = new List<WidgetPlacementDto>();
		foreach (var placement in placements)
		{
			if (!Enum.IsDefined(placement.Tool) || !Enum.IsDefined(placement.Dock))
				continue;
			if (string.IsNullOrWhiteSpace(placement.Id))
				placement.Id = GetDefaultId(placement.Tool);
			if (result.Any(x => x.Id == placement.Id))
				continue;
			if (
				!AllowsMultipleInstances(placement.Tool)
				&& result.Any(x => x.Tool == placement.Tool)
			)
				continue;
			result.Add(placement);
		}
		return result;
	}
}
