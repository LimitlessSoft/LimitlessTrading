using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Settings;

public class LayoutEntityMapper : ILSCoreMapper<LayoutDto, LayoutEntity>
{
	public LayoutEntity ToMapped(LayoutDto source) =>
		new()
		{
			LeftWidth = source.LeftWidth,
			RightWidth = source.RightWidth,
			BottomHeight = source.BottomHeight,
			LeftWidgetId = source.LeftWidgetId,
			RightWidgetId = source.RightWidgetId,
			BottomWidgetId = source.BottomWidgetId,
			Widgets = source.Widgets.Select(new WidgetPlacementEntityMapper().ToMapped).ToList(),
			Center = source.Center is { } center
				? new CenterNodeEntityMapper().ToMapped(center)
				: null,
			ChartTimeframe = source.ChartTimeframe,
			WindowWidth = source.WindowWidth,
			WindowHeight = source.WindowHeight,
			IsMaximized = source.IsMaximized,
		};
}
