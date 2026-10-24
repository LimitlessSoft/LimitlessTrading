using IBApi;
using LSCore.Mapper.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;

namespace LT.Ibkr.DtoMappings.Contracts;

/// <summary>LT contract → IB contract. A resolved contract is identified by conId + exchange.</summary>
public class IbContractMapper : ILSCoreMapper<ContractDto, Contract>
{
	public Contract ToMapped(ContractDto source) =>
		source.ConId > 0
			? new Contract
			{
				ConId = source.ConId,
				Exchange = string.IsNullOrEmpty(source.Exchange)
					? MarketDataConstants.DefaultExchange
					: source.Exchange,
			}
			: new Contract
			{
				Symbol = source.Symbol,
				SecType = source.SecurityType,
				Exchange = source.Exchange,
				PrimaryExch = source.PrimaryExchange,
				Currency = source.Currency,
				LocalSymbol = source.LocalSymbol,
				LastTradeDateOrContractMonth = source.LastTradeDateOrContractMonth,
				// IB reads 0 as "strike 0" (error 200); "any strike" is its unset value
				Strike = source.Strike > 0 ? source.Strike : double.MaxValue,
				Right = source.Right,
				Multiplier = source.Multiplier,
				TradingClass = source.TradingClass,
			};
}
