using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Contracts;
using Contract = IBKR.Net.Contract;

namespace LT.Ibkr.DtoMappings.Contracts;

public class IbkrContractDtoMapper : ILSCoreMapper<Contract, ContractDto>
{
	public ContractDto ToMapped(Contract source) =>
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
			Strike = source.Strike ?? 0,
			Right = source.Right,
			Multiplier = source.Multiplier,
			TradingClass = source.TradingClass,
		};
}
