using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Settings;

public class CenterViewDtoMapper : ILSCoreMapper<CenterViewEntity, CenterViewDto>
{
	public CenterViewDto ToMapped(CenterViewEntity source) =>
		new()
		{
			Id = source.Id,
			Name = source.Name,
			Root = source.Root is { } root ? new CenterNodeDtoMapper().ToMapped(root) : null,
		};
}
