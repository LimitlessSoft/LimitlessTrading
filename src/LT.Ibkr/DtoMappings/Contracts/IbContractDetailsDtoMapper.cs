using IBApi;
using LSCore.Mapper.Contracts;
using LSCore.Mapper.Domain;
using LT.Contracts.Dtos.Contracts;

namespace LT.Ibkr.DtoMappings.Contracts;

public class IbContractDetailsDtoMapper : ILSCoreMapper<ContractDetails, ContractDetailsDto>
{
	public ContractDetailsDto ToMapped(ContractDetails source) =>
		new()
		{
			Contract = source.Contract.ToMapped<Contract, ContractDto>(),
			LongName = source.LongName ?? string.Empty,
			MinTick = source.MinTick,
			TimeZoneId = source.TimeZoneId ?? string.Empty,
			Industry = source.Industry ?? string.Empty,
			Category = source.Category ?? string.Empty,
			ContractMonth = source.ContractMonth ?? string.Empty,
		};
}
