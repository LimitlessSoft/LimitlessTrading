using LSCore.Mapper.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using Contract = IBKR.Net.Contract;

namespace LT.Ibkr.DtoMappings.Contracts;

/// <summary>
/// LT contract → IBKR.Net contract. A resolved contract is identified by conId + exchange; a broad
/// tape news contract keeps its symbol and security type too.
/// </summary>
public class IbkrContractMapper : ILSCoreMapper<ContractDto, Contract>
{
	public Contract ToMapped(ContractDto source) =>
		// A broad tape is subscribed with the provider code as exchange and its security type
		source.SecurityType == NewsConstants.NewsSecurityType
			? new Contract
			{
				ConId = source.ConId,
				Symbol = source.Symbol,
				SecurityType = source.SecurityType,
				Exchange = source.Exchange,
			}
		: source.ConId > 0
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
			SecurityType = source.SecurityType,
			Exchange = source.Exchange,
			PrimaryExchange = source.PrimaryExchange,
			Currency = source.Currency,
			LocalSymbol = source.LocalSymbol,
			LastTradeDateOrContractMonth = source.LastTradeDateOrContractMonth,
			// No strike is "any strike"; IB reads 0 as strike 0 (error 200)
			Strike = source.Strike > 0 ? source.Strike : null,
			Right = source.Right,
			Multiplier = source.Multiplier,
			TradingClass = source.TradingClass,
		};
}
