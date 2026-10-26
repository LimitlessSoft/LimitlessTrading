namespace LT.Contracts.Constants;

public static class PriceAlertConstants
{
	/// <summary>The alerts, active and triggered, in the app-data folder.</summary>
	public const string FileName = "price-alerts.json";

	public const int MaxNoteLength = 200;

	/// <summary>Active alerts across every contract.</summary>
	public const int MaxActiveAlerts = 200;

	/// <summary>
	/// Contracts with an active alert: each is watched on its own market data line (counted by
	/// IMarketDataManager against <see cref="MarketDataConstants.MaxLines"/>).
	/// </summary>
	public const int MaxWatchedContracts = 20;

	/// <summary>A contract's price stream that failed or ended is opened again after this.</summary>
	public static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

	/// <summary>How long a triggered alert's notification stays on screen.</summary>
	public static readonly TimeSpan NotificationDuration = TimeSpan.FromSeconds(15);
}
