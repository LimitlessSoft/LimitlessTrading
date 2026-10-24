using LT.Contracts.Dtos.Account;

namespace LT.Contracts.Interfaces.IManagers;

public interface IAccountManager
{
	Task<AccountSummaryDto> GetSummaryAsync(
		string account,
		CancellationToken cancellationToken = default
	);

	IAsyncEnumerable<AccountPnlDto> SubscribePnlAsync(
		string account,
		CancellationToken cancellationToken = default
	);
}
