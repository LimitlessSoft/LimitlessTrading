using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Settings;

public class LayoutDtoMapper : ILSCoreMapper<LayoutEntity, LayoutDto>
{
	public LayoutDto ToMapped(LayoutEntity source) =>
		WorkspaceLayoutMapping.Fill(
			new LayoutDto
			{
				ChartTimeframe = source.ChartTimeframe,
				WindowWidth = source.WindowWidth,
				WindowHeight = source.WindowHeight,
				IsMaximized = source.IsMaximized,
				WindowX = source.WindowX,
				WindowY = source.WindowY,
				WindowState = source.WindowState,
				WindowScreen = source.WindowScreen is { } screen
					? new WindowScreenDtoMapper().ToMapped(screen)
					: null,
				Windows = source.Windows.Select(new WorkspaceWindowDtoMapper().ToMapped).ToList(),
			},
			source
		);
}
