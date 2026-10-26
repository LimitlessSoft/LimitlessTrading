using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Executions;

public class ExecutionDtoMapper : ILSCoreMapper<FillEntity, ExecutionDto>
{
	public ExecutionDto ToMapped(FillEntity source) =>
		new()
		{
			ExecId = source.ExecId,
			Time = source.Time,
			Account = source.Account,
			ConId = source.ConId,
			Symbol = source.Symbol,
			LocalSymbol = source.LocalSymbol,
			SecurityType = source.SecurityType,
			Multiplier = source.Multiplier,
			Side = source.Side,
			Shares = source.Shares,
			Price = source.Price,
			Exchange = source.Exchange,
			OrderId = source.OrderId,
			Commission = source.Commission,
			CommissionCurrency = source.CommissionCurrency,
			RealizedPnl = source.RealizedPnl,
		};
}
