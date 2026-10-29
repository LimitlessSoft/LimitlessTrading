using IBKR.Net;
using LSCore.Mapper.Contracts;
using LSCore.Mapper.Domain;
using LT.Contracts.Dtos.Contracts;
using Contract = IBKR.Net.Contract;

namespace LT.Ibkr.DtoMappings.Contracts;

public class ContractDetailsDtoMapper : ILSCoreMapper<ContractDetails, ContractDetailsDto>
{
	public ContractDetailsDto ToMapped(ContractDetails source) =>
		new()
		{
			Contract = source.Contract.ToMapped<Contract, ContractDto>(),
			LongName = source.LongName,
			MinTick = source.MinTick,
			TimeZoneId = source.TimeZoneId,
			TradingHours = source.TradingHours,
			LiquidHours = source.LiquidHours,
			Industry = source.Industry,
			Category = source.Category,
			Subcategory = source.Subcategory,
			ContractMonth = source.ContractMonth,
			MarketName = source.MarketName,
			ValidExchanges = source.ValidExchanges,
			UnderlyingConId = source.UnderlyingConId,
			SecurityIds = new Dictionary<string, string>(source.SecurityIds),
		};
}
