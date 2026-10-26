using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Settings;

/// <summary>
/// Copies the workspace part shared by the main layout and every extra window
/// (<see cref="WorkspaceLayoutEntity"/> ⇄ <see cref="WorkspaceLayoutDto"/>).
/// </summary>
internal static class WorkspaceLayoutMapping
{
	public static T Fill<T>(T target, WorkspaceLayoutEntity source)
		where T : WorkspaceLayoutDto
	{
		target.LeftWidth = source.LeftWidth;
		target.RightWidth = source.RightWidth;
		target.BottomHeight = source.BottomHeight;
		target.LeftWidgetId = source.LeftWidgetId;
		target.RightWidgetId = source.RightWidgetId;
		target.BottomWidgetId = source.BottomWidgetId;
		target.Widgets = source.Widgets.Select(new WidgetPlacementDtoMapper().ToMapped).ToList();
		target.Views = source.Views.Select(new CenterViewDtoMapper().ToMapped).ToList();
		target.SelectedViewId = source.SelectedViewId;
		target.Center = source.Center is { } center
			? new CenterNodeDtoMapper().ToMapped(center)
			: null;
		return target;
	}

	public static T Fill<T>(T target, WorkspaceLayoutDto source)
		where T : WorkspaceLayoutEntity
	{
		target.LeftWidth = source.LeftWidth;
		target.RightWidth = source.RightWidth;
		target.BottomHeight = source.BottomHeight;
		target.LeftWidgetId = source.LeftWidgetId;
		target.RightWidgetId = source.RightWidgetId;
		target.BottomWidgetId = source.BottomWidgetId;
		target.Widgets = source.Widgets.Select(new WidgetPlacementEntityMapper().ToMapped).ToList();
		target.Views = source.Views.Select(new CenterViewEntityMapper().ToMapped).ToList();
		target.SelectedViewId = source.SelectedViewId;
		target.Center = source.Center is { } center
			? new CenterNodeEntityMapper().ToMapped(center)
			: null;
		return target;
	}
}
