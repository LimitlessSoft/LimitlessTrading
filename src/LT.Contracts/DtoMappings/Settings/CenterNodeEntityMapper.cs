using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Settings;

public class CenterNodeEntityMapper : ILSCoreMapper<CenterNodeDto, CenterNodeEntity>
{
	public CenterNodeEntity ToMapped(CenterNodeDto source) =>
		new()
		{
			WidgetId = source.WidgetId,
			Orientation = source.Orientation,
			Size = source.Size,
			Children = source.Children.Select(ToMapped).ToList(),
		};
}
