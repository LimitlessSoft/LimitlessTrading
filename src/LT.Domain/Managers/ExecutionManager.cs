using LT.Contracts.Dtos.Executions;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;

namespace LT.Domain.Managers;

public class ExecutionManager(IIbkrApiClient ibkrApiClient) : IExecutionManager
{
	public async Task<IReadOnlyList<ExecutionDto>> GetExecutionsAsync(
		string? account,
		CancellationToken cancellationToken = default
	)
	{
		if (!ibkrApiClient.IsConnected)
			return [];

		var executions = await ibkrApiClient.GetExecutionsAsync(cancellationToken);
		return executions
			.Where(x => string.IsNullOrEmpty(account) || x.Account == account)
			.OrderByDescending(x => x.Time)
			.ToList();
	}
}
