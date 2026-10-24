using System.Runtime.CompilerServices;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Account;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;

namespace LT.Domain.Managers;

public class AccountManager(IIbkrApiClient ibkrApiClient) : IAccountManager
{
	public async Task<AccountSummaryDto> GetSummaryAsync(
		string account,
		CancellationToken cancellationToken = default
	)
	{
		var values = await ibkrApiClient.GetAccountSummaryAsync(
			AccountConstants.SummaryTags,
			cancellationToken
		);
		return AccountHelpers.BuildSummary(account, values, DateTimeOffset.Now);
	}

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
