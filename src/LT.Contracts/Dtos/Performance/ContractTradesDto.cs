using LT.Contracts.Dtos.Executions;

namespace LT.Contracts.Dtos.Performance;

/// <summary>The recorded fills of one contract and how they matched up (for the chart).</summary>
public class ContractTradesDto
{
	/// <summary>Every recorded fill of the contract, oldest first.</summary>
	public IReadOnlyList<ExecutionDto> Fills { get; set; } = [];

	/// <summary>Entry → exit pairs of the closed quantity (FIFO), by exit time.</summary>
	public IReadOnlyList<TradeLegDto> Legs { get; set; } = [];
}
