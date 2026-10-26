namespace LT.Contracts.Constants;

public static class NewsConstants
{
	/// <summary>IB returns at most 300 past headlines per reqHistoricalNews.</summary>
	public const int MaxHeadlinesPerRequest = 300;

	/// <summary>Past headlines asked for at once (first load and every "Load older").</summary>
	public const int HeadlinesPageSize = 100;

	/// <summary>Headlines a news widget keeps; the oldest drop off as new ones arrive.</summary>
	public const int MaxHeadlinesShown = 500;

	/// <summary>Articles kept in memory, so going back to a headline doesn't ask IB again.</summary>
	public const int ArticleCacheSize = 50;
	public static readonly TimeSpan ArticleCacheTtl = TimeSpan.FromMinutes(30);

	/// <summary>Provider chip clicks within this time restart the feed once.</summary>
	public static readonly TimeSpan ProviderChangeDelay = TimeSpan.FromMilliseconds(400);

	public const string IbkrSourceId = "ibkr";
	public const string IbkrSourceName = "Interactive Brokers";

	/// <summary>IB's security type of broad tape contracts (a provider's whole feed).</summary>
	public const string NewsSecurityType = "NEWS";

	/// <summary>A provider's broad tape covering all of its sub-tapes, e.g. BRFG:BRFG_ALL.</summary>
	public const string AllTapeSuffix = "_ALL";

	/// <summary>IB errors meaning "this provider has no broad tape" (321 invalid source, 200 no definition).</summary>
	public static readonly IReadOnlyList<int> NoBroadTapeErrorCodes = [321, 200];

	/// <summary>Separates source id and provider code in a saved provider key ("ibkr:BRFG").</summary>
	public const char ProviderKeySeparator = ':';
}
