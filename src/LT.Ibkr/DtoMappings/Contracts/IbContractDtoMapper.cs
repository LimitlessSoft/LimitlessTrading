using IBApi;
using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Contracts;

namespace LT.Ibkr.DtoMappings.Contracts;

public class IbContractDtoMapper : ILSCoreMapper<Contract, ContractDto>
{
	public ContractDto ToMapped(Contract source) =>
		new()
		{
			ConId = source.ConId,
			Symbol = source.Symbol ?? string.Empty,
			LocalSymbol = source.LocalSymbol ?? string.Empty,
			SecurityType = source.SecType ?? string.Empty,
			Exchange = source.Exchange ?? string.Empty,
			PrimaryExchange = source.PrimaryExch ?? string.Empty,
			Currency = source.Currency ?? string.Empty,
			LastTradeDateOrContractMonth = source.LastTradeDateOrContractMonth ?? string.Empty,
			Strike = source.Strike,
			Right = source.Right ?? string.Empty,
			Multiplier = source.Multiplier ?? string.Empty,
			TradingClass = source.TradingClass ?? string.Empty,
		};
}
