using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Executions;

/// <summary>An IB fill as a journal entry; the caller sets the time if IB sent none, and RecordedAt.</summary>
public class FillEntityMapper : ILSCoreMapper<ExecutionDto, FillEntity>
{
	public FillEntity ToMapped(ExecutionDto source) =>
		new()
		{
			ExecId = source.ExecId,
			Time = source.Time ?? default,
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
