using IBApi;
using LSCore.Mapper.Contracts;
using LSCore.Mapper.Domain;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Positions;
using LT.Contracts.Helpers;
using TwsApi;

namespace LT.Ibkr.DtoMappings.Positions;

public class PositionDtoMapper : ILSCoreMapper<PositionInfo, PositionDto>
{
	public PositionDto ToMapped(PositionInfo source)
	{
		var contract = source.Contract.ToMapped<Contract, ContractDto>();
		return new PositionDto
		{
			Account = source.Account,
			ContractId = contract.ConId,
			Symbol = ContractHelpers.GetDisplayName(contract),
			SecurityType = contract.SecurityType,
			Currency = contract.Currency,
			Exchange = ContractHelpers.GetListingExchange(contract),
			Quantity = source.Position,
			AverageCost = source.AvgCost,
			Contract = contract,
		};
	}
}
