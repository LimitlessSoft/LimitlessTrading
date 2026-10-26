using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Settings;

public class CenterViewEntityMapper : ILSCoreMapper<CenterViewDto, CenterViewEntity>
{
	public CenterViewEntity ToMapped(CenterViewDto source) =>
		new()
		{
			Id = source.Id,
			Name = source.Name,
			Root = source.Root is { } root ? new CenterNodeEntityMapper().ToMapped(root) : null,
		};
}
