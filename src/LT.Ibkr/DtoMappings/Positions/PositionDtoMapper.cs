using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Positions;
using TwsApi;

namespace LT.Ibkr.DtoMappings.Positions;

public class PositionDtoMapper : ILSCoreMapper<PositionInfo, PositionDto>
{
	public PositionDto ToMapped(PositionInfo source) =>
		new()
		{
			Account = source.Account,
			ContractId = source.Contract.ConId,
			Symbol = string.IsNullOrEmpty(source.Contract.LocalSymbol)
				? source.Contract.Symbol
				: source.Contract.LocalSymbol,
			SecurityType = source.Contract.SecType,
			Currency = source.Contract.Currency,
			Exchange = string.IsNullOrEmpty(source.Contract.PrimaryExch)
				? source.Contract.Exchange
				: source.Contract.PrimaryExch,
			Quantity = source.Position,
			AverageCost = source.AvgCost,
		};
}
