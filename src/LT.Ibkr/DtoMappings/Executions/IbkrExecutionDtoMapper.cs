using System.Globalization;
using LSCore.Mapper.Contracts;
using LSCore.Mapper.Domain;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Helpers;
using Contract = IBKR.Net.Contract;
using Execution = IBKR.Net.Execution;

namespace LT.Ibkr.DtoMappings.Executions;

/// <summary>
/// A fill from reqExecutions with its commission report. Side stays IB's "BOT" / "SLD" with a
/// positive quantity; the time falls back to IB's own text when IBKR.Net doesn't know its zone.
/// </summary>
public class IbkrExecutionDtoMapper : ILSCoreMapper<Execution, ExecutionDto>
{
	public ExecutionDto ToMapped(Execution source)
	{
		// The contract's exchange is where it filled, not where to ask for data: leave it to SMART
		var contract = source.Contract.ToMapped<Contract, ContractDto>();
		contract.Exchange = string.Empty;
		return new()
		{
			ExecId = source.ExecId,
			Time = source.Time ?? IbTimeHelpers.Parse(source.TimeText),
			Account = source.Account,
			ConId = source.Contract.ConId,
			Symbol = source.Contract.Symbol,
			LocalSymbol = source.Contract.LocalSymbol,
			SecurityType = source.Contract.SecurityType,
			Multiplier =
				double.TryParse(
					source.Contract.Multiplier,
					NumberStyles.Float,
					CultureInfo.InvariantCulture,
					out var multiplier
				)
				&& multiplier > 0
					? multiplier
					: null,
			Side = source.Side,
			Shares = source.Shares,
			Price = source.Price,
			Exchange = source.Exchange,
			OrderId = source.OrderId,
			Commission = source.Commission?.Commission,
			CommissionCurrency = source.Commission?.Currency ?? string.Empty,
			RealizedPnl = source.Commission?.RealizedPnl,
			Contract = contract,
		};
	}
}
