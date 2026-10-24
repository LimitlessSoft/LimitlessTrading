using IBKR.Net;
using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Positions;

namespace LT.Ibkr.DtoMappings.Positions;

public class PositionPnlDtoMapper : ILSCoreMapper<PositionPnl, PositionPnlDto>
{
	public PositionPnlDto ToMapped(PositionPnl source) =>
		new()
		{
			Position = source.Position,
			DailyPnl = source.DailyPnl,
			UnrealizedPnl = source.UnrealizedPnl,
			RealizedPnl = source.RealizedPnl,
			MarketValue = source.MarketValue,
		};
}
