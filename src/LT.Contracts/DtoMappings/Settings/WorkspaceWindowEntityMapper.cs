using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Settings;

public class WorkspaceWindowEntityMapper : ILSCoreMapper<WorkspaceWindowDto, WorkspaceWindowEntity>
{
	public WorkspaceWindowEntity ToMapped(WorkspaceWindowDto source) =>
		WorkspaceLayoutMapping.Fill(
			new WorkspaceWindowEntity
			{
				Id = source.Id,
				Name = source.Name,
				X = source.X,
				Y = source.Y,
				Width = source.Width,
				Height = source.Height,
				IsMaximized = source.IsMaximized,
			},
			source
		);
}
