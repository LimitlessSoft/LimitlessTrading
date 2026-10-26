using System.Runtime.CompilerServices;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Account;
using LT.Contracts.Dtos.Cache;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;

namespace LT.Domain.Managers;

public class AccountManager(IIbkrApiClient ibkrApiClient, ICacheManager cacheManager)
	: IAccountManager
{
	public async IAsyncEnumerable<AccountSummaryDto> SubscribeSummaryAsync(
		string account,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var values = new Dictionary<string, AccountValueDto>();
		await foreach (var value in ibkrApiClient.SubscribeAccountSummaryAsync(cancellationToken))
		{
			if (value.Account != account)
				continue;
			values[value.Tag] = value;
			var summary = AccountHelpers.BuildSummary(account, values.Values, DateTimeOffset.Now);
			cacheManager.Save(
				CacheType.Snapshots,
				CacheHelpers.GetAccountKey(CacheConstants.AccountSummarySnapshot, account),
				summary
			);
			yield return summary;
		}
	}

	public CachedDto<AccountSummaryDto>? GetSummarySnapshot(string account) =>
		cacheManager.GetStale<AccountSummaryDto>(
			CacheType.Snapshots,
			CacheHelpers.GetAccountKey(CacheConstants.AccountSummarySnapshot, account)
		);

	public async IAsyncEnumerable<AccountPnlDto> SubscribePnlAsync(
		string account,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		await foreach (
			var pnl in ibkrApiClient.SubscribeAccountPnlAsync(account, cancellationToken)
		)
			yield return new AccountPnlDto
			{
				DailyPnl = IbValueHelpers.Clean(pnl.DailyPnl),
				UnrealizedPnl = IbValueHelpers.Clean(pnl.UnrealizedPnl),
				RealizedPnl = IbValueHelpers.Clean(pnl.RealizedPnl),
			};
	}
}
