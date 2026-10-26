using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Settings;

public class WindowScreenEntityMapper : ILSCoreMapper<WindowScreenDto, WindowScreenEntity>
{
	public WindowScreenEntity ToMapped(WindowScreenDto source) =>
		new()
		{
			Name = source.Name,
			X = source.X,
			Y = source.Y,
			Width = source.Width,
			Height = source.Height,
		};
}
