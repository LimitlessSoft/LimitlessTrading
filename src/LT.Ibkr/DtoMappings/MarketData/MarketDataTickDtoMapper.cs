using IBKR.Net;
using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.MarketData;

namespace LT.Ibkr.DtoMappings.MarketData;

public class MarketDataTickDtoMapper : ILSCoreMapper<MarketDataTick, MarketDataTickDto>
{
	public MarketDataTickDto ToMapped(MarketDataTick source) =>
		new()
		{
			Field = source.Field,
			Price = source.Price,
			Size = source.Size,
			Value = source.Value,
			StringValue = source.StringValue,
		};
}
