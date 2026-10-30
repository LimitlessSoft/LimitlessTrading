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
			Option = source.Option is { } option
				? new OptionComputationDto
				{
					ImpliedVolatility = option.ImpliedVolatility,
					Delta = option.Delta,
					OptionPrice = option.OptionPrice,
					PresentValueDividend = option.PresentValueDividend,
					Gamma = option.Gamma,
					Vega = option.Vega,
					Theta = option.Theta,
					UnderlyingPrice = option.UnderlyingPrice,
				}
				: null,
		};
}
