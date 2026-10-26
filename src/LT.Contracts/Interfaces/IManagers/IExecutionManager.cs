using LT.Contracts.Dtos.Cache;
using LT.Contracts.Dtos.Executions;

namespace LT.Contracts.Interfaces.IManagers;

public interface IExecutionManager
{
	/// <summary>
	/// Today's fills, cached briefly per account. Disconnected, or when IB fails, the account's
	/// last cached fills come back with IsStale set (none without an account or a cached copy).
	/// </summary>
	Task<CachedDto<IReadOnlyList<ExecutionDto>>> GetExecutionsAsync(
		string? account,
		CancellationToken cancellationToken = default
	);
}
