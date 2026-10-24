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
	public const int RecentContractsMax = 10;
	public const int TickerSuggestionsMax = 20;
	public const int SymbolMaxLength = 32;
	public const int OptionStrikesAroundAtm = 8;
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
