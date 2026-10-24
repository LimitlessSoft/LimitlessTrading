using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Settings;

public class CenterNodeDtoMapper : ILSCoreMapper<CenterNodeEntity, CenterNodeDto>
{
	public CenterNodeDto ToMapped(CenterNodeEntity source) =>
		new()
		{
			WidgetId = source.WidgetId,
			Orientation = source.Orientation,
			Size = source.Size,
			Children = source.Children.Select(ToMapped).ToList(),
		};
}
