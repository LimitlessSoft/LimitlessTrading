using IBKR.Net;
using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.MarketDepth;

namespace LT.Ibkr.DtoMappings.MarketData;

public class MarketDepthUpdateDtoMapper : ILSCoreMapper<MarketDepthUpdate, MarketDepthUpdateDto>
{
	public MarketDepthUpdateDto ToMapped(MarketDepthUpdate source) =>
		new()
		{
			Position = source.Position,
			Operation = (LT.Contracts.Enums.MarketDepthOperation)source.Operation,
			Side = (LT.Contracts.Enums.MarketDepthSide)source.Side,
			Price = source.Price,
			Size = source.Size,
			MarketMaker = source.MarketMaker,
			IsSmartDepth = source.IsSmartDepth,
		};
}
