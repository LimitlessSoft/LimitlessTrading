namespace LT.Contracts.Constants;

public static class MarketDataConstants
{
	/// <summary>IB default is 100 simultaneous lines per account; keep headroom for the chart.</summary>
	public const int MaxLines = 90;

	/// <summary>IB allows 5 simultaneous tick-by-tick subscriptions per account (error 10190).</summary>
	public const int MaxTickByTick = 5;

	/// <summary>IB disconnects above ~50 messages/s; stay below it.</summary>
	public const int MaxMessagesPerSecond = 40;

	/// <summary>Historical data pacing for bars of 30 s or less: 60 requests in 10 minutes.</summary>
	public const int MaxHistoricalRequests = 60;
	public static readonly TimeSpan HistoricalWindow = TimeSpan.FromMinutes(10);

	/// <summary>How often coalesced quotes are pushed to the UI.</summary>
	public static readonly TimeSpan UiFlushInterval = TimeSpan.FromMilliseconds(250);

	/// <summary>
	/// How often a watchlist sorted by a quote column re-sorts while quotes tick: at most once a
	/// second, so rows don't jump on every flush.
	/// </summary>
	public static readonly TimeSpan WatchlistResortInterval = TimeSpan.FromSeconds(1);

	public const int WatchlistMaxItems = 200;

	/// <summary>
	/// Quote snapshots running at once, app-wide. IB doesn't count them against the lines
	/// (recorded, docs/ibkr-tws-api.md#quote-snapshots), but each stays open about 11 s.
	/// </summary>
	public const int MaxConcurrentSnapshots = 20;

	/// <summary>A snapshot IB hasn't ended by then fails (IB ends them after about 11 s).</summary>
	public static readonly TimeSpan SnapshotTimeout = TimeSpan.FromSeconds(20);

	/// <summary>
	/// Rows above and below the ones on screen that stream live too, so a short scroll shows
	/// live quotes at once.
	/// </summary>
	public const int WatchlistLiveRowBuffer = 5;

	/// <summary>Rows streamed live before the grid has reported which rows it shows.</summary>
	public const int WatchlistInitialLiveRows = 30;

	/// <summary>
	/// A row scrolled away keeps its line this long, so scrolling back and forth doesn't
	/// resubscribe.
	/// </summary>
	public static readonly TimeSpan WatchlistLiveReleaseDelay = TimeSpan.FromSeconds(3);

	/// <summary>
	/// Lines the watchlist leaves free for charts, the option chain and alerts: it opens no live
	/// row when fewer than this are left (those rows get snapshots instead).
	/// </summary>
	public const int WatchlistReservedLines = 10;

	/// <summary>How often a watchlist looks for off-screen rows due a snapshot.</summary>
	public static readonly TimeSpan WatchlistSnapshotCheckInterval = TimeSpan.FromSeconds(2);

	/// <summary>Snapshots a watchlist widget keeps running at once (each takes about 11 s).</summary>
	public const int WatchlistSnapshotsInFlight = 10;

	/// <summary>An off-screen row's snapshot is refreshed once it is this old (oldest first).</summary>
	public static readonly TimeSpan WatchlistSnapshotRefreshInterval = TimeSpan.FromSeconds(30);

	/// <summary>A row whose snapshot failed (e.g. no subscription) is asked again after this.</summary>
	public static readonly TimeSpan WatchlistSnapshotErrorRetry = TimeSpan.FromMinutes(5);
	public const int WatchlistNameMaxLength = 40;

	/// <summary>Name of the first watchlist (and of one migrated from older settings).</summary>
	public const string DefaultWatchlistName = "Main";

	/// <summary>Name of the watchlist a fresh install starts with (US stocks).</summary>
	public const string StarterWatchlistName = "US";

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
	/// Generic tick list for the 90-day average daily volume (tick 21, in shares like tick 8's
	/// volume) on a quote line; the watchlist's Avg Vol column uses it.
	/// </summary>
	public const string AverageVolumeGenericTick = "165";

	/// <summary>
	/// Generic tick list for IB's trade rate and volume rate (ticks 55 and 56, per minute, as
	/// generic ticks); the watchlist's Vol/Min column uses the volume rate.
	/// </summary>
	public const string VolumeRateGenericTick = "295";

	/// <summary>
	/// Minutes of a regular session the average daily volume is spread over for the watchlist's
	/// Avg Vol/Min column: the US one (09:30–16:00 New York), for every listing.
	/// </summary>
	public const int RegularSessionMinutes = 390;

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
	public const string FutureOptionType = "FOP";
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
