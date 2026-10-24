using IBKR.Net;
using LSCore.Mapper.Contracts;
using LSCore.Mapper.Domain;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Positions;
using LT.Contracts.Helpers;
using Contract = IBKR.Net.Contract;

namespace LT.Ibkr.DtoMappings.Positions;

public class PositionDtoMapper : ILSCoreMapper<Position, PositionDto>
{
	public PositionDto ToMapped(Position source)
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
			Quantity = source.Quantity,
			AverageCost = source.AverageCost,
			Contract = contract,
		};
	}
}
