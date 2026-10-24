using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Watchlists;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Watchlists;

public class WatchlistDtoMapper : ILSCoreMapper<WatchlistEntity, WatchlistDto>
{
	public WatchlistDto ToMapped(WatchlistEntity source) =>
		new()
		{
			Id = source.Id,
			Name = source.Name,
			Count = source.Items.Count,
		};
}
