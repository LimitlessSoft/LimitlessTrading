using LT.Contracts.Dtos.Executions;

namespace LT.Contracts.Interfaces.IManagers;

public interface IExecutionManager
{
	Task<IReadOnlyList<ExecutionDto>> GetExecutionsAsync(
		string? account,
		CancellationToken cancellationToken = default
	);
}
