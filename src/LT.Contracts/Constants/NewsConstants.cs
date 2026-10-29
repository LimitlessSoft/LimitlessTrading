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

	/// <summary>
	/// Providers without a broad tape of their own whose headlines all come on another provider's
	/// tape (exchange, symbol). The Dow Jones API providers share <c>DJ:N_DJN</c>, which carries
	/// DJ-N, DJ-RT, DJ-RTA, DJ-RTE and DJ-RTG mixed; IB rejects them as exchange (321). DJ has
	/// ~7,200 topic tapes, so the contract is built here rather than looked up.
	/// <c>DJ:S_ALL</c>, <c>DJ:M_ALL</c> and the DJTOP tapes miss headlines: don't use them.
	/// </summary>
	public static readonly IReadOnlyDictionary<
		string,
		(string Exchange, string Symbol)
	> SharedBroadTapes = new Dictionary<string, (string Exchange, string Symbol)>
	{
		["DJ-N"] = ("DJ", "DJ:N_DJN"),
		["DJ-RT"] = ("DJ", "DJ:N_DJN"),
		["DJ-RTA"] = ("DJ", "DJ:N_DJN"),
		["DJ-RTE"] = ("DJ", "DJ:N_DJN"),
		["DJ-RTG"] = ("DJ", "DJ:N_DJN"),
	};

	/// <summary>IB errors meaning "this provider has no broad tape" (321 invalid source, 200 no definition).</summary>
	public static readonly IReadOnlyList<int> NoBroadTapeErrorCodes = [321, 200];

	/// <summary>Separates source id and provider code in a saved provider key ("ibkr:BRFG").</summary>
	public const char ProviderKeySeparator = ':';

	/// <summary>Days the local news archive keeps by default; whole days are deleted past it on start.</summary>
	public const int DefaultRetentionDays = 365;

	/// <summary>0 keeps everything.</summary>
	public const int RetentionDaysMin = 0;
	public const int RetentionDaysMax = 3650;

	/// <summary>Folder under the app data directory: <c>news/&lt;yyyy-MM-dd&gt;.jsonl</c>, one line per headline.</summary>
	public const string ArchiveDirectoryName = "news";

	/// <summary>Under the archive folder: <c>articles/&lt;yyyy-MM-dd&gt;.jsonl</c>, articles read once.</summary>
	public const string ArticlesDirectoryName = "articles";

	/// <summary>The newest days whose headlines the archive keeps in memory; older ones are read on demand.</summary>
	public const int ArchiveCachedDays = 3;

	/// <summary>
	/// The same story from two sources (a press release on PR Newswire and on IB's Dow Jones) is
	/// shown once: same headline words within this time.
	/// </summary>
	public static readonly TimeSpan StoryWindow = TimeSpan.FromMinutes(10);

	/// <summary>A new matching headline older than this (a feed's backlog on start) plays no sound.</summary>
	public static readonly TimeSpan AlertMaxAge = TimeSpan.FromMinutes(15);

	public const int AlertKeywordsMaxLength = 500;

	/// <summary>Shortest archive search.</summary>
	public const int SearchMinLength = 2;

	/// <summary>Typing in Search mode searches once the typing pauses this long.</summary>
	public static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(400);

	/// <summary>A source saved in a widget's provider selection with no provider selected ("lt:").</summary>
	public static string GetSourceMarker(string sourceId) => $"{sourceId}{ProviderKeySeparator}";
}
