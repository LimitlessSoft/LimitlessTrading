using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Contracts;

public class ContractEntityMapper : ILSCoreMapper<ContractDto, ContractEntity>
{
	public ContractEntity ToMapped(ContractDto source) =>
		new()
		{
			ConId = source.ConId,
			Symbol = source.Symbol,
			LocalSymbol = source.LocalSymbol,
			SecurityType = source.SecurityType,
			Exchange = source.Exchange,
			PrimaryExchange = source.PrimaryExchange,
			Currency = source.Currency,
			LastTradeDateOrContractMonth = source.LastTradeDateOrContractMonth,
			Strike = source.Strike,
			Right = source.Right,
			Multiplier = source.Multiplier,
			TradingClass = source.TradingClass,
		};
}
