using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Settings;

public class WorkspaceWindowDtoMapper : ILSCoreMapper<WorkspaceWindowEntity, WorkspaceWindowDto>
{
	public WorkspaceWindowDto ToMapped(WorkspaceWindowEntity source) =>
		WorkspaceLayoutMapping.Fill(
			new WorkspaceWindowDto
			{
				Id = source.Id,
				Name = source.Name,
				X = source.X,
				Y = source.Y,
				Width = source.Width,
				Height = source.Height,
				IsMaximized = source.IsMaximized,
				State = source.State,
				Screen = source.Screen is { } screen
					? new WindowScreenDtoMapper().ToMapped(screen)
					: null,
			},
			source
		);
}
