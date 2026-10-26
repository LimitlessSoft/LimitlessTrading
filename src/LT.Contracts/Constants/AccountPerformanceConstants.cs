namespace LT.Contracts.Constants;

public static class AccountPerformanceConstants
{
	/// <summary>
	/// Snapshots are bucketed by the date in New York: IB's trading day for US markets, the same
	/// for every user whatever their own time zone (a session never splits across two days).
	/// </summary>
	public const string TradingDayTimeZoneId = "America/New_York";

	/// <summary>Today's snapshot changes with every P&amp;L tick; disk is written at most this often.</summary>
	public static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(30);

	public const string HistoryDirectoryName = "account-history";

	/// <summary>Cells of the month grid: six Monday-first weeks, so its height never changes.</summary>
	public const int CalendarWeeks = 6;
}
