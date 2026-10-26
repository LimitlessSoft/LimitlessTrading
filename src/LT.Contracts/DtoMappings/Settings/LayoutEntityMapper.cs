using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Settings;

public class LayoutEntityMapper : ILSCoreMapper<LayoutDto, LayoutEntity>
{
	public LayoutEntity ToMapped(LayoutDto source) =>
		WorkspaceLayoutMapping.Fill(
			new LayoutEntity
			{
				ChartTimeframe = source.ChartTimeframe,
				WindowWidth = source.WindowWidth,
				WindowHeight = source.WindowHeight,
				IsMaximized = source.IsMaximized,
				Windows = source
					.Windows.Select(new WorkspaceWindowEntityMapper().ToMapped)
					.ToList(),
			},
			source
		);
}
