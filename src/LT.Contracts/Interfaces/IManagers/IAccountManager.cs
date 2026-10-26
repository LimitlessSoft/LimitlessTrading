using LT.Contracts.Dtos.Account;
using LT.Contracts.Dtos.Cache;

namespace LT.Contracts.Interfaces.IManagers;

public interface IAccountManager
{
	/// <summary>
	/// The account's summary now, then again every time one of its values changes; the latest is
	/// kept as a snapshot.
	/// </summary>
	IAsyncEnumerable<AccountSummaryDto> SubscribeSummaryAsync(
		string account,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// The account's last-known summary (always IsStale), for display only while live data is
	/// unavailable; never for decisions. Null when there is none.
	/// </summary>
	CachedDto<AccountSummaryDto>? GetSummarySnapshot(string account);

	IAsyncEnumerable<AccountPnlDto> SubscribePnlAsync(
		string account,
		CancellationToken cancellationToken = default
	);
}
