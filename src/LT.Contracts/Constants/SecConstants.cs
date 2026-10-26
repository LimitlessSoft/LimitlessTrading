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

	/// <summary>A count older than this is ignored (BRK's last cover-page count is from 2011).</summary>
	public static readonly TimeSpan MaxFactAge = TimeSpan.FromDays(400);

	/// <summary>A found count is looked up again after this long.</summary>
	public static readonly TimeSpan SharesOutstandingTtl = TimeSpan.FromDays(7);

	/// <summary>A ticker the SEC has nothing usable for (ETFs, foreign filers) is asked again after this long.</summary>
	public static readonly TimeSpan SharesNotFoundTtl = TimeSpan.FromDays(3);

	/// <summary>After a failed lookup (network, SEC down) no new lookup starts for this long.</summary>
	public static readonly TimeSpan FailureBackoff = TimeSpan.FromMinutes(5);

	public const string SharesOutstandingFileName = "shares-outstanding.json";
}
