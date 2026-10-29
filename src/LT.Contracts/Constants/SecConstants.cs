namespace LT.Contracts.Constants;

/// <summary>
/// SEC EDGAR, where the watchlist's market cap gets shares outstanding from (IB's fundamentals
/// tick 258 needs a subscription and otherwise rejects the whole quote line, error 10358).
/// </summary>
public static class SecConstants
{
	/// <summary>Every listed ticker with its CIK (one file, ~1 MB, fetched once per run).</summary>
	public const string CompanyTickersUrl = "https://www.sec.gov/files/company_tickers.json";

	/// <summary>One XBRL concept of one company; {0} is the CIK padded to ten digits.</summary>
	public const string SharesOutstandingConceptUrl =
		"https://data.sec.gov/api/xbrl/companyconcept/CIK{0}/dei/EntityCommonStockSharesOutstanding.json";

	/// <summary>Every XBRL fact of one company (several MB): the fallback when the concept is missing.</summary>
	public const string CompanyFactsUrl = "https://data.sec.gov/api/xbrl/companyfacts/CIK{0}.json";

	/// <summary>A company's filings, newest first; {0} is the CIK padded to ten digits.</summary>
	public const string SubmissionsUrl = "https://data.sec.gov/submissions/CIK{0}.json";

	/// <summary>
	/// The XBRL instance EDGAR extracts from an inline-XBRL filing (1–8 MB): {0} the CIK, {1} the
	/// accession number without dashes, {2} the primary document's name without ".htm".
	/// </summary>
	public const string FilingInstanceUrl =
		"https://www.sec.gov/Archives/edgar/data/{0}/{1}/{2}_htm.xml";

	/// <summary>
	/// EDGAR's latest filings as Atom, newest first (what its "Latest Filings" page lists): {0}
	/// the form ("8-K"), {1} the 0-based start. Each entry names the CIK in its title, the 8-K
	/// items in its summary and the acceptance time in <c>updated</c> (checked 2026-10-29).
	/// </summary>
	public const string CurrentFilingsUrl =
		"https://www.sec.gov/cgi-bin/browse-edgar?action=getcurrent&type={0}&company=&dateb=&owner=include&start={1}&count=100&output=atom";

	/// <summary>Entries per page of <see cref="CurrentFilingsUrl"/> (EDGAR's largest).</summary>
	public const int CurrentFilingsPageSize = 100;

	/// <summary>The form companies file their earnings release with.</summary>
	public const string CurrentReportForm = "8-K";

	/// <summary>8-K Item 2.02, Results of Operations and Financial Condition: the earnings release.</summary>
	public const string ResultsOfOperationsItem = "2.02";

	/// <summary>
	/// The SEC blocks automated requests (403) without a User-Agent naming the tool and a contact
	/// e-mail address.
	/// </summary>
	public const string UserAgentProduct = "LimitlessTrading";
	public const string UserAgentContact = "office@limitlesssoft.com";

	/// <summary>SEC fair access: at most 10 requests per second; kept to 8.</summary>
	public static readonly TimeSpan RequestSpacing = TimeSpan.FromMilliseconds(125);
	public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(30);

	public const string SharesUnit = "shares";
	public const string DeiTaxonomy = "dei";
	public const string SharesOutstandingConcept = "EntityCommonStockSharesOutstanding";
	public const string UsGaapTaxonomy = "us-gaap";
	public const string CommonStockSharesOutstandingConcept = "CommonStockSharesOutstanding";
	public const string WeightedAverageBasicConcept =
		"WeightedAverageNumberOfSharesOutstandingBasic";
	public const string WeightedAverageDilutedConcept =
		"WeightedAverageNumberOfDilutedSharesOutstanding";

	/// <summary>The XBRL dimension a filing's per-class counts are reported on.</summary>
	public const string ClassOfStockAxis = "StatementClassOfStockAxis";
	public const string DeiNamespacePrefix = "http://xbrl.sec.gov/dei/";
	public const string XbrlInstanceNamespace = "http://www.xbrl.org/2003/instance";
	public const string XbrlDimensionsNamespace = "http://xbrl.org/2006/xbrldi";

	/// <summary>
	/// Per-class counts that aren't one share of the ticker each, by SEC ticker then class member:
	/// how many of the ticker's shares one share of the class is. A class not listed counts as one.
	/// Berkshire: one Class A converts into 1,500 Class B.
	/// </summary>
	public static readonly IReadOnlyDictionary<
		string,
		IReadOnlyDictionary<string, decimal>
	> ShareClassRatios = new Dictionary<string, IReadOnlyDictionary<string, decimal>>(
		StringComparer.OrdinalIgnoreCase
	)
	{
		["BRK-B"] = new Dictionary<string, decimal> { ["CommonClassAMember"] = 1500m },
		["BRK-A"] = new Dictionary<string, decimal> { ["CommonClassBMember"] = 1m / 1500m },
	};

	/// <summary>
	/// Only US filers' reports count: a 20-F / 40-F counts ordinary shares, which an ADR's price
	/// doesn't match (one TSM ADR is five shares).
	/// </summary>
	public static readonly IReadOnlyList<string> SharesOutstandingForms =
	[
		"10-K",
		"10-Q",
		"10-K/A",
		"10-Q/A",
		"10-KT",
	];

	/// <summary>
	/// A count older than this is ignored (BRK's last undivided cover-page count is from 2011).
	/// </summary>
	public static readonly TimeSpan MaxFactAge = TimeSpan.FromDays(400);

	/// <summary>A found count is looked up again after this long.</summary>
	public static readonly TimeSpan SharesOutstandingTtl = TimeSpan.FromDays(7);

	/// <summary>A ticker the SEC has nothing usable for (ETFs, foreign filers) is asked again after this long.</summary>
	public static readonly TimeSpan SharesNotFoundTtl = TimeSpan.FromDays(3);

	/// <summary>
	/// Raised when the lookup finds more than before, so a cached "nothing usable" is asked again
	/// right away. 1: weighted averages and the latest filing's per-class cover-page counts.
	/// </summary>
	public const int SharesLookupVersion = 1;

	/// <summary>After a failed lookup (network, SEC down) no new lookup starts for this long.</summary>
	public static readonly TimeSpan FailureBackoff = TimeSpan.FromMinutes(5);

	public const string SharesOutstandingFileName = "shares-outstanding.json";
}
