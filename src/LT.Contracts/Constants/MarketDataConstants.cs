namespace LT.Contracts.Constants;

public static class MarketDataConstants
{
	/// <summary>IB default is 100 simultaneous lines per account; keep headroom for the chart.</summary>
	public const int MaxLines = 90;

	/// <summary>IB disconnects above ~50 messages/s; stay below it.</summary>
	public const int MaxMessagesPerSecond = 40;

	/// <summary>Historical data pacing: no more than 60 requests in 10 minutes.</summary>
	public const int MaxHistoricalRequests = 60;
	public static readonly TimeSpan HistoricalWindow = TimeSpan.FromMinutes(10);

	/// <summary>How often coalesced quotes are pushed to the UI.</summary>
	public static readonly TimeSpan UiFlushInterval = TimeSpan.FromMilliseconds(250);

	public const int WatchlistMaxItems = 200;
	public const int WatchlistNameMaxLength = 40;

	/// <summary>Name of the first watchlist (and of one migrated from older settings).</summary>
	public const string DefaultWatchlistName = "Main";

	/// <summary>New watchlists are "Watchlist 2", "Watchlist 3" …</summary>
	public const string NewWatchlistNamePrefix = "Watchlist";
	public const int RecentContractsMax = 10;
	public const int TickerSuggestionsMax = 20;
	public const int SymbolMaxLength = 32;
	public const int OptionStrikesAroundAtm = 8;

	/// <summary>
	/// The chain's strike window re-centres once the ATM strike is more than this many strikes
	/// from its centre, so a price wobbling around a strike doesn't churn subscriptions.
	/// </summary>
	public const int OptionStrikesRecenterThreshold = 3;

	/// <summary>
	/// Generic tick list for the underlying's option implied volatility (tick 24) on its quote
	/// line; the option chain's standard deviation bands use it.
	/// </summary>
	public const string OptionImpliedVolatilityGenericTick = "106";

	/// <summary>
	/// Options are taken to expire at the US close (16:00 New York) of their expiry date when
	/// working out the time left for the standard deviation bands.
	/// </summary>
	public static readonly TimeSpan OptionExpirationClose = TimeSpan.FromHours(16);
	public const string OptionExpirationTimeZone = "America/New_York";
	public const double DaysPerYear = 365;
	public static readonly TimeSpan UnderlyingPriceTimeout = TimeSpan.FromSeconds(5);

	public const string StockType = "STK";
	public const string OptionType = "OPT";
	public const string FutureType = "FUT";
	public const string ForexType = "CASH";
	public const string IndexType = "IND";
	public const string CryptoType = "CRYPTO";

	public const string DefaultExchange = "SMART";

	/// <summary>
	/// Listings in this currency rank first in search, so a bare ticker (SPY) resolves to the US
	/// listing rather than e.g. SPY on ASX in AUD, which sorts earlier alphabetically.
	/// </summary>
	public const string PreferredCurrency = "USD";
	public const string CallRight = "C";

	/// <summary>Weekly options trade under the symbol plus W, e.g. SPXW for SPX.</summary>
	public const string WeeklyTradingClassSuffix = "W";
	public const string PutRight = "P";

	/// <summary>
	/// What a typed symbol is looked up as (ticker boxes), in result order. Futures (every contract
	/// month) and forex (symbol = base currency) don't fit a plain symbol search.
	/// </summary>
	public static readonly IReadOnlyList<string> SymbolSearchTypes =
	[
		StockType,
		IndexType,
		CryptoType,
	];

	public static readonly IReadOnlyList<string> SearchableSecurityTypes =
	[
		StockType,
		FutureType,
		ForexType,
		IndexType,
		CryptoType,
	];
}
