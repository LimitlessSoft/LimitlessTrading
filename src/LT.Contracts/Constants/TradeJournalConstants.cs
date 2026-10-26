namespace LT.Contracts.Constants;

/// <summary>The local trade journal: recorded fills and intraday P&amp;L samples.</summary>
public static class TradeJournalConstants
{
	/// <summary>Under the app-data folder: <c>trade-journal/fills</c> and <c>trade-journal/pnl</c>.</summary>
	public const string DirectoryName = "trade-journal";
	public const string FillsDirectoryName = "fills";
	public const string PnlDirectoryName = "pnl";

	/// <summary>
	/// Days are New York trading days, so a US session (and its after-hours) stays on one date
	/// wherever the user lives. Falls back to the local zone when the id is unknown.
	/// </summary>
	public const string TradeDayTimeZoneId = "America/New_York";

	/// <summary>At most one curve point per account this often; a newer value replaces it.</summary>
	public static readonly TimeSpan PnlSampleInterval = TimeSpan.FromSeconds(10);

	/// <summary>How often new curve points are written to disk (and on disconnect / exit).</summary>
	public static readonly TimeSpan PnlSaveInterval = TimeSpan.FromMinutes(1);

	/// <summary>Accounts whose P&amp;L is recorded per connection (one reqPnL each).</summary>
	public const int MaxPnlAccounts = 10;
}
