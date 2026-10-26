using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Settings;

public class WindowScreenDtoMapper : ILSCoreMapper<WindowScreenEntity, WindowScreenDto>
{
	public WindowScreenDto ToMapped(WindowScreenEntity source) =>
		new()
		{
			Name = source.Name,
			X = source.X,
			Y = source.Y,
			Width = source.Width,
			Height = source.Height,
		};
}
