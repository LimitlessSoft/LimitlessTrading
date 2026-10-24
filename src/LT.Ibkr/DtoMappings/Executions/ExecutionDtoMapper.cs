using IBApi;
using LSCore.Mapper.Contracts;
using LSCore.Mapper.Domain;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Helpers;
using TwsApi;

namespace LT.Ibkr.DtoMappings.Executions;

public class ExecutionDtoMapper : ILSCoreMapper<ExecutionInfo, ExecutionDto>
{
	public ExecutionDto ToMapped(ExecutionInfo source)
	{
		var contract = source.Contract.ToMapped<Contract, ContractDto>();
		return new ExecutionDto
		{
			ExecId = source.Execution.ExecId ?? string.Empty,
			Time = IbTimeHelpers.Parse(source.Execution.Time),
			Account = source.Execution.AcctNumber ?? string.Empty,
			Symbol = ContractHelpers.GetDisplayName(contract),
			SecurityType = contract.SecurityType,
			Side = source.Execution.Side ?? string.Empty,
			Shares = source.Execution.Shares,
			Price = source.Execution.Price,
			Exchange = source.Execution.Exchange ?? string.Empty,
			OrderId = source.Execution.OrderId,
			Commission = IbValueHelpers.Clean(source.CommissionAndFees?.CommissionAndFees),
			CommissionCurrency = source.CommissionAndFees?.Currency ?? string.Empty,
			RealizedPnl = IbValueHelpers.Clean(source.CommissionAndFees?.RealizedPNL),
		};
	}
}
