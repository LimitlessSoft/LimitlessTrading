using LT.Contracts.Dtos.News;
using LT.Contracts.Enums;

namespace LT.Contracts.Constants;

/// <summary>
/// The app's own news source: public feeds the app polls itself and keeps in the local archive
/// (docs/news-feeds.md). Every URL here answered a plain GET with <see cref="UserAgent"/> on
/// 2026-10-29.
/// </summary>
public static class FeedNewsConstants
{
	public const string SourceId = "lt";
	public const string SourceName = "LimitlessTrading";

	/// <summary>The SEC blocks requests (403) without a tool name and a contact address; every feed accepts it.</summary>
	public static string UserAgent(string version) =>
		$"{SecConstants.UserAgentProduct}/{version} {SecConstants.UserAgentContact}";

	public const string Accept =
		"application/rss+xml, application/atom+xml, application/xml;q=0.9, text/xml;q=0.9, */*;q=0.5";

	public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(20);

	/// <summary>Least time between two requests to one host (the SEC allows 10 a second).</summary>
	public static readonly TimeSpan HostRequestSpacing = TimeSpan.FromMilliseconds(250);

	/// <summary>A symbol's feeds are fetched for history at most this often (more widgets, Load older).</summary>
	public static readonly TimeSpan SymbolFetchTtl = TimeSpan.FromMinutes(5);

	/// <summary>The SEC ticker → CIK file is asked again after a failure only after this long.</summary>
	public static readonly TimeSpan TickerMapRetry = TimeSpan.FromMinutes(10);

	/// <summary>A feed summary is cut to this many characters (some wires put the whole release in it).</summary>
	public const int MaxSummaryLength = 1500;

	/// <summary>Largest page read as article text (a filing index, a filing's press release exhibit).</summary>
	public const int MaxArticleBytes = 2_000_000;

	/// <summary>Entries of an EDGAR company feed page; a full page means older filings exist.</summary>
	public const int SecPageSize = 40;

	public const string SecCode = "SEC";

	/// <summary>EDGAR's <c>dateb</c> parameter: filings on or before this date.</summary>
	public const string SecDateFormat = "yyyyMMdd";

	/// <summary>Short names of 8-K items for headlines; 9.01 (exhibits) says nothing and is left out.</summary>
	public static readonly IReadOnlyDictionary<string, string> SecItemNames = new Dictionary<
		string,
		string
	>
	{
		["1.01"] = "Material agreement",
		["1.02"] = "Agreement ended",
		["1.03"] = "Bankruptcy",
		["1.05"] = "Cybersecurity incident",
		["2.01"] = "Acquisition / disposal",
		["2.02"] = "Results",
		["2.03"] = "New debt",
		["2.04"] = "Debt accelerated",
		["2.05"] = "Exit costs",
		["2.06"] = "Impairment",
		["3.01"] = "Delisting notice",
		["3.02"] = "Unregistered equity sale",
		["3.03"] = "Holders' rights changed",
		["4.01"] = "Auditor change",
		["4.02"] = "Non-reliance on financials",
		["5.01"] = "Change in control",
		["5.02"] = "Officers / directors",
		["5.03"] = "Bylaws / fiscal year",
		["5.07"] = "Shareholder vote",
		["5.08"] = "Director nominations",
		["7.01"] = "Reg FD",
		["8.01"] = "Other events",
	};

	public const string SecExhibitItem = "9.01";

	/// <summary>
	/// Routine fund and administrative forms that don't move a stock: left out of the SEC feeds
	/// when fetched and when read from the archive. Amendments ("/A") too. Insider Form 4 stays;
	/// the initial and annual ownership statements (3, 5) go.
	/// </summary>
	public static readonly IReadOnlySet<string> SecExcludedForms = new HashSet<string>(
		StringComparer.OrdinalIgnoreCase
	)
	{
		"N-PORT",
		"NPORT-P",
		"NPORT-EX",
		"NPORT-NP",
		"497",
		"497K",
		"497J",
		"497AD",
		"24F-2NT",
		"N-CEN",
		"N-30D",
		"N-30B-2",
		"485BPOS",
		"485APOS",
		"485BXT",
		"N-CSR",
		"N-CSRS",
		"N-MFP",
		"N-MFP2",
		"N-MFP3",
		"N-PX",
		"N-Q",
		"40-17G",
		"13F-HR",
		"13F-NT",
		"SC 13G",
		"3",
		"5",
	};

	/// <summary>Exhibit types that are the filing's press release, read with it as the article.</summary>
	public static readonly IReadOnlyList<string> SecPressReleaseTypes = ["EX-99.1", "EX-99"];

	/// <summary>
	/// The providers. SEC, PR Newswire, the Fed and BLS are official and free to use: on by default.
	/// The others are unofficial endpoints or for personal use only: off until picked as a chip.
	/// GlobeNewswire (403 to plain clients), Google News (terms forbid automated use), Nasdaq news
	/// (no answer without a browser) and ACCESS Newswire (no feed) are left out.
	/// </summary>
	public static readonly IReadOnlyList<NewsFeedDefinitionDto> Feeds =
	[
		new()
		{
			Code = SecCode,
			Name = "SEC EDGAR filings",
			Kind = NewsFeedKind.Sec,
			BroadUrls =
			[
				"https://www.sec.gov/cgi-bin/browse-edgar?action=getcurrent&type=8-K&company=&dateb=&owner=include&start=0&count=40&output=atom",
			],
			SymbolUrl =
				"https://www.sec.gov/cgi-bin/browse-edgar?action=getcompany&CIK={0}&type=&dateb={1}&owner=include&count=40&output=atom",
			TagsSymbols = true,
			PollInterval = TimeSpan.FromSeconds(30),
			SelectedByDefault = true,
		},
		new()
		{
			Code = "PRN",
			Name = "PR Newswire",
			BroadUrls = ["https://www.prnewswire.com/rss/news-releases-list.rss"],
			TagsSymbols = true,
			PollInterval = TimeSpan.FromSeconds(60),
			SelectedByDefault = true,
		},
		new()
		{
			Code = "FED",
			Name = "Federal Reserve",
			BroadUrls = ["https://www.federalreserve.gov/feeds/press_all.xml"],
			PollInterval = TimeSpan.FromSeconds(60),
			SelectedByDefault = true,
		},
		new()
		{
			Code = "BLS",
			Name = "Bureau of Labor Statistics",
			// One feed per release: jobs, CPI, PPI, JOLTS, employment cost, real earnings, import prices
			BroadUrls =
			[
				"https://www.bls.gov/feed/empsit.rss",
				"https://www.bls.gov/feed/cpi.rss",
				"https://www.bls.gov/feed/ppi.rss",
				"https://www.bls.gov/feed/jolts.rss",
				"https://www.bls.gov/feed/eci.rss",
				"https://www.bls.gov/feed/realer.rss",
				"https://www.bls.gov/feed/ximpim.rss",
			],
			PollInterval = TimeSpan.FromMinutes(2),
			SelectedByDefault = true,
		},
		new()
		{
			Code = "BW",
			Name = "Business Wire (earnings)",
			BroadUrls = ["https://feed.businesswire.com/rss/home/?rss=G1QFDERJXkJeEF9YXA=="],
			TagsSymbols = true,
			PollInterval = TimeSpan.FromMinutes(2),
		},
		new()
		{
			Code = "FDA",
			Name = "FDA press releases",
			BroadUrls =
			[
				"https://www.fda.gov/about-fda/contact-fda/stay-informed/rss-feeds/press-releases/rss.xml",
			],
			PollInterval = TimeSpan.FromMinutes(10),
		},
		new()
		{
			Code = "CNBC",
			Name = "CNBC top news",
			BroadUrls =
			[
				"https://search.cnbc.com/rs/search/combinedcms/view.xml?partnerId=wrss01&id=100003114",
			],
			PollInterval = TimeSpan.FromMinutes(2),
		},
		new()
		{
			Code = "MW",
			Name = "MarketWatch top stories",
			BroadUrls = ["https://feeds.content.dowjones.io/public/rss/mw_topstories"],
			PollInterval = TimeSpan.FromMinutes(2),
		},
		new()
		{
			Code = "YF",
			Name = "Yahoo Finance",
			SymbolUrl =
				"https://feeds.finance.yahoo.com/rss/2.0/headline?s={0}&region=US&lang=en-US",
			PollInterval = TimeSpan.FromMinutes(2),
		},
		new()
		{
			Code = "SA",
			Name = "Seeking Alpha",
			SymbolUrl = "https://seekingalpha.com/api/sa/combined/{0}.xml",
			PollInterval = TimeSpan.FromMinutes(5),
		},
	];
}
