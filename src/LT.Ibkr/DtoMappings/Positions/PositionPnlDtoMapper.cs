using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Positions;
using TwsApi;

namespace LT.Ibkr.DtoMappings.Positions;

public class PositionPnlDtoMapper : ILSCoreMapper<PositionPnl, PositionPnlDto>
{
	public PositionPnlDto ToMapped(PositionPnl source) =>
		new()
		{
			Position = source.Position,
			DailyPnl = source.DailyPnL,
			UnrealizedPnl = source.UnrealizedPnL,
			RealizedPnl = source.RealizedPnL,
			MarketValue = source.MarketValue,
		};
}
