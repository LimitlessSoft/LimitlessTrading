using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Settings;

public class LayoutDtoMapper : ILSCoreMapper<LayoutEntity, LayoutDto>
{
	public LayoutDto ToMapped(LayoutEntity source) =>
		new()
		{
			LeftWidth = source.LeftWidth,
			RightWidth = source.RightWidth,
			BottomHeight = source.BottomHeight,
			LeftWidgetId = source.LeftWidgetId,
			RightWidgetId = source.RightWidgetId,
			BottomWidgetId = source.BottomWidgetId,
			Widgets = source.Widgets.Select(new WidgetPlacementDtoMapper().ToMapped).ToList(),
			Center = source.Center is { } center
				? new CenterNodeDtoMapper().ToMapped(center)
				: null,
			ChartTimeframe = source.ChartTimeframe,
			WindowWidth = source.WindowWidth,
			WindowHeight = source.WindowHeight,
			IsMaximized = source.IsMaximized,
		};
}
