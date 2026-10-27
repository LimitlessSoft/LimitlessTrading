using IBKR.Net;
using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Calendar;

namespace LT.Ibkr.DtoMappings.Calendar;

/// <summary>A market-wide WSH query: a filter, no contract, no watchlist / portfolio fill.</summary>
public class WshEventDataRequestMapper : ILSCoreMapper<WshEventQueryDto, WshEventDataRequest>
{
	public WshEventDataRequest ToMapped(WshEventQueryDto source) =>
		new()
		{
			Filter = source.Filter,
			StartDate = source.StartDate,
			EndDate = source.EndDate,
			TotalLimit = source.TotalLimit,
		};
}
