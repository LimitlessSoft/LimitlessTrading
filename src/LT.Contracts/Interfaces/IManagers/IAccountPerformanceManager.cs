using LT.Contracts.Dtos.AccountPerformance;

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

	/// <summary>Today's trading day (the date in New York).</summary>
	DateOnly GetToday();
}
