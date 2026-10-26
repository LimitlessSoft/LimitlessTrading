using LT.Contracts.Constants;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

public static class WorkspaceHelpers
{
	// Not part of the first-run workspace
	private static readonly WorkspaceTool[] OpenedOnPurpose =
	[
		WorkspaceTool.OptionChain,
		WorkspaceTool.OrderTicket,
		WorkspaceTool.News,
		WorkspaceTool.AccountPerformance,
		WorkspaceTool.DayTradePerformance,
	];

	/// <summary>Where a widget opens when it is spawned and has no saved placement.</summary>
	public static WidgetDock GetDefaultDock(WorkspaceTool tool) =>
		tool switch
		{
			WorkspaceTool.Watchlist
			or WorkspaceTool.Positions
			or WorkspaceTool.Orders
			or WorkspaceTool.Executions => WidgetDock.Left,
			WorkspaceTool.Account or WorkspaceTool.Messages or WorkspaceTool.OrderTicket =>
				WidgetDock.Right,
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
	/// First-run workspace: every widget except the option chain, the order ticket, news, account
	/// performance and day trade performance (opened on purpose, from the menu), in its default dock.
	/// </summary>
	public static List<WidgetPlacementEntity> GetDefaultPlacements() =>
		Enum.GetValues<WorkspaceTool>()
			.Where(x => !OpenedOnPurpose.Contains(x))
			.Select(x => new WidgetPlacementEntity
			{
				Id = GetDefaultId(x),
				Tool = x,
				Dock = GetDefaultDock(x),
			})
			.ToList();

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
