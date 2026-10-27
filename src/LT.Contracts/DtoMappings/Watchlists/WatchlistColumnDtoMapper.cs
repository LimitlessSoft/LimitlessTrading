using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Watchlists;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Watchlists;

public class WatchlistColumnDtoMapper : ILSCoreMapper<WatchlistColumnEntity, WatchlistColumnDto>
{
	public WatchlistColumnDto ToMapped(WatchlistColumnEntity source) =>
		new()
		{
			Id = source.Id,
			Name = source.Name,
			Cells =
			[
				.. source.Cells.Select(x => new WatchlistCellDto
				{
					ConId = x.ConId,
					Exchange = x.Exchange,
					Value = x.Value,
				}),
			],
		};
}
