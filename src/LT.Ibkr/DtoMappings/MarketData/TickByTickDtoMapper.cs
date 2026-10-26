using IBKR.Net;
using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.MarketData;

namespace LT.Ibkr.DtoMappings.MarketData;

public class TickByTickDtoMapper : ILSCoreMapper<TickByTick, TickByTickDto>
{
	public TickByTickDto ToMapped(TickByTick source) =>
		new()
		{
			Type = (LT.Contracts.Enums.TickByTickType)(int)source.Type,
			Time = source.Time,
			Price = source.Price,
			Size = source.Size,
			BidPrice = source.BidPrice,
			AskPrice = source.AskPrice,
			BidSize = source.BidSize,
			AskSize = source.AskSize,
			IsUnreported = source.Unreported,
		};
}
