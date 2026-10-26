namespace LT.Contracts.Constants;

public static class CacheConstants
{
	public const string CacheDirectoryName = "cache";

	/// <summary>Where the chart cache lived before the general cache; deleted on start.</summary>
	public const string LegacyBarCacheDirectoryName = "bar-cache";

	public const int DefaultMaxSizeMb = 500;
	public const int MaxSizeMbMin = 10;
	public const int MaxSizeMbMax = 10240;
	public const long BytesPerMb = 1024L * 1024;

	/// <summary>Contract definitions barely change; a symbol search is served from disk this long.</summary>
	public static readonly TimeSpan ContractTtl = TimeSpan.FromDays(3);

	/// <summary>
	/// A contract's trading hours: shorter than <see cref="ContractTtl"/>, as IB lists only the
	/// coming days (holidays, early closes) and the regular session is read from them.
	/// </summary>
	public static readonly TimeSpan TradingHoursTtl = TimeSpan.FromHours(12);

	/// <summary>Expirations and strike lists (never prices or greeks).</summary>
	public static readonly TimeSpan OptionChainTtl = TimeSpan.FromDays(1);

	/// <summary>Short: a refresh right after a fill must still show it.</summary>
	public static readonly TimeSpan ExecutionsTtl = TimeSpan.FromSeconds(10);

	// Kinds of per-account snapshots (CacheType.Snapshots) and of the executions cache
	public const string PositionsSnapshot = "positions";
	public const string OpenOrdersSnapshot = "orders";
	public const string AccountSummarySnapshot = "summary";
	public const string ExecutionsKind = "executions";
}
