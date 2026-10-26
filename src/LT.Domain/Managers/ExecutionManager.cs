using LT.Contracts.Constants;
using LT.Contracts.Dtos.Cache;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;

namespace LT.Domain.Managers;

public class ExecutionManager(
	IIbkrApiClient ibkrApiClient,
	ICacheManager cacheManager,
	TimeProvider timeProvider
) : IExecutionManager
{
	public async Task<CachedDto<IReadOnlyList<ExecutionDto>>> GetExecutionsAsync(
		string? account,
		CancellationToken cancellationToken = default
	)
	{
		// Only one account's fills are ever cached under its key, never all accounts together
		var key = string.IsNullOrEmpty(account)
			? null
			: CacheHelpers.GetAccountKey(CacheConstants.ExecutionsKind, account);

		if (!ibkrApiClient.IsConnected)
			return (
					key == null
						? null
						: cacheManager.GetStale<IReadOnlyList<ExecutionDto>>(
							CacheType.Executions,
							key
						)
				)
				?? new CachedDto<IReadOnlyList<ExecutionDto>>
				{
					Value = [],
					FetchedAt = timeProvider.GetUtcNow(),
				};

		async Task<IReadOnlyList<ExecutionDto>> FetchAsync(CancellationToken ct) =>
			(await ibkrApiClient.GetExecutionsAsync(ct))
				.Where(x => string.IsNullOrEmpty(account) || x.Account == account)
				.OrderByDescending(x => x.Time)
				.ToList();

		if (key == null)
			return new CachedDto<IReadOnlyList<ExecutionDto>>
			{
				Value = await FetchAsync(cancellationToken),
				FetchedAt = timeProvider.GetUtcNow(),
			};
		return await cacheManager.GetOrFetchAsync(
			CacheType.Executions,
			key,
			CacheConstants.ExecutionsTtl,
			FetchAsync,
			cancellationToken
		);
	}
}
