using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Options;
using TwsApi;

namespace LT.Ibkr.DtoMappings.Options;

public class OptionChainParametersDtoMapper
	: ILSCoreMapper<OptionChainParameters, OptionChainParametersDto>
{
	public OptionChainParametersDto ToMapped(OptionChainParameters source) =>
		new()
		{
			Exchange = source.Exchange,
			TradingClass = source.TradingClass,
			Multiplier = source.Multiplier,
			Expirations = source.Expirations,
			Strikes = source.Strikes,
		};
}
