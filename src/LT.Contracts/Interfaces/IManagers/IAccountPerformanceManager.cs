using LT.Contracts.Dtos.AccountPerformance;
using LT.Contracts.Dtos.Flex;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>
/// Records one snapshot per account and trading day (net liquidation, daily / realized /
/// unrealized P&amp;L) whenever the app is connected, and serves that history. IB's socket API has
/// no NAV or P&amp;L history, so history starts the first day the app ran.
/// </summary>
public interface IAccountPerformanceManager
{
	/// <summary>Raised on any thread with the account whose today snapshot changed.</summary>
	event Action<string>? HistoryChanged;

	/// <summary>
	/// Records every managed account while connected, until <paramref name="stoppingToken"/> is
	/// cancelled; writes to disk at most every AccountPerformanceConstants.SaveInterval and once
	/// more on disconnect and on stop. Run once, by the app's host.
	/// </summary>
	Task RecordAsync(CancellationToken stoppingToken);

	/// <summary>The recorded days of <paramref name="account"/>, oldest first, today included live.</summary>
	IReadOnlyList<AccountDaySnapshotDto> GetHistory(string account);

	/// <summary>
	/// The deposits, withdrawals and transfers of <paramref name="account"/> imported from Flex,
	/// oldest first.
	/// </summary>
	IReadOnlyList<CashMovementDto> GetCashMovements(string account);

	/// <summary>
	/// Merges end-of-day NAV, deposits / withdrawals and transfers from a Flex Activity statement
	/// into the history and saves it. Flex wins for days before today; today stays live. A Flex
	/// day's daily P&amp;L is its NAV change minus the money moved in / out that day
	/// (<paramref name="cashFlows"/> and <paramref name="transfers"/>, the same money in both
	/// counted once). Every deposit, withdrawal and transfer is kept as a cash movement. Returns
	/// the days and movements added or changed.
	/// </summary>
	int ImportFlexHistory(
		IReadOnlyList<FlexNavDto> nav,
		IReadOnlyList<FlexCashFlowDto> cashFlows,
		IReadOnlyList<FlexCashFlowDto> transfers
	);

	/// <summary>Today's trading day (the date in New York).</summary>
	DateOnly GetToday();
}
