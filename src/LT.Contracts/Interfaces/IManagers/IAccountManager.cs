using LT.Contracts.Dtos.Account;

namespace LT.Contracts.Interfaces.IManagers;

public interface IAccountManager
{
	/// <summary>The account's summary now, then again every time one of its values changes.</summary>
	IAsyncEnumerable<AccountSummaryDto> SubscribeSummaryAsync(
		string account,
		CancellationToken cancellationToken = default
	);

	IAsyncEnumerable<AccountPnlDto> SubscribePnlAsync(
		string account,
		CancellationToken cancellationToken = default
	);
}
