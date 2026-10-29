using LT.Contracts.Constants;
using LT.Contracts.Dtos.Feeds;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Feeds;
using LT.Tests.Feeds;

namespace LT.Tests.Contracts.Helpers;

public class NewsFeedHelpersTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 29, 21, 30, 0, TimeSpan.Zero);

	private static readonly IReadOnlyDictionary<int, IReadOnlyList<string>> CikTickers =
		NewsFeedHelpers.InvertTickerCiks(
			new Dictionary<string, int>
			{
				["SSTK"] = 1549346,
				["ATR"] = 896622,
				["GOOGL"] = 1652044,
				["GOOG"] = 1652044,
			}
		);

	[Theory]
	[InlineData("Millrose Properties, Inc. (\"Millrose\") (NYSE: MRP) for violations", "MRP")]
	[InlineData("Kimberly-Clark Corporation (NASDAQ: KMB) (\"Kimberly-Clark\")", "KMB")]
	[InlineData("IQM Quantum Computers (Nasdaq: IQMX), a leader", "IQMX")]
	[InlineData("Berkshire (NYSE: BRK.B) today", "BRK-B")]
	[InlineData("listed on NYSE American: XYZ and", "XYZ")]
	[InlineData("(Nasdaq GS: ABCD)", "ABCD")]
	public void Press_releases_name_their_ticker_with_the_exchange(string text, string ticker) =>
		Assert.Equal([ticker], NewsFeedHelpers.ExtractTickers(text));

	[Fact]
	public void Several_tickers_are_kept_once_each_in_order() =>
		Assert.Equal(
			["BAC", "MS"],
			NewsFeedHelpers.ExtractTickers("(NYSE: BAC) and (NYSE: MS); again (NYSE: BAC)")
		);

	[Theory]
	[InlineData("(TSX: ABC)")]
	[InlineData("NYSE today said")]
	[InlineData("")]
	public void Text_without_a_us_listing_has_no_ticker(string text) =>
		Assert.Empty(NewsFeedHelpers.ExtractTickers(text));

	[Fact]
	public void An_edgar_title_gives_form_company_and_cik()
	{
		Assert.Equal(
			("8-K", "Shutterstock, Inc.", 1549346, (string?)null),
			NewsFeedHelpers.ParseSecTitle("8-K - Shutterstock, Inc. (0001549346) (Filer)")
		);
		Assert.Equal(
			(
				"4",
				(string?)null,
				(int?)null,
				"Statement of changes in beneficial ownership of securities"
			),
			NewsFeedHelpers.ParseSecTitle(
				"4  - Statement of changes in beneficial ownership of securities"
			)
		);
	}

	[Fact]
	public void A_current_8k_becomes_a_headline_with_the_ticker_and_what_its_items_are_about()
	{
		var item = FeedParser.Parse(FeedFixtures.Open("sec-current-8k.xml")).Items[1];

		var headline = NewsFeedHelpers.ToHeadline(Sec, item, null, CikTickers, Now)!;

		Assert.Equal(
			"Shutterstock, Inc. (SSTK) 8-K: Officers / directors, Reg FD",
			headline.Headline
		);
		Assert.Equal("SSTK", headline.Symbol);
		Assert.Equal(["SSTK"], headline.Symbols);
		Assert.Equal(FeedNewsConstants.SourceId, headline.SourceId);
		Assert.Equal("SEC", headline.ProviderCode);
		Assert.Equal(item.Id, headline.ArticleId);
		Assert.Equal(item.Url, headline.Url);
	}

	[Theory]
	[InlineData("NPORT-P", true)]
	[InlineData("497K", true)]
	[InlineData("24F-2NT", true)]
	[InlineData("N-CSRS", true)]
	[InlineData("485BPOS", true)]
	[InlineData("SC 13G/A", true)]
	[InlineData("13F-HR", true)]
	[InlineData("3", true)]
	[InlineData("5/A", true)]
	[InlineData("4", false)]
	[InlineData("8-K", false)]
	[InlineData("10-Q", false)]
	[InlineData("SC 13D", false)]
	public void Routine_forms_are_left_out(string form, bool excluded) =>
		Assert.Equal(excluded, NewsFeedHelpers.IsExcludedSecForm(form));

	[Fact]
	public void A_routine_filing_is_dropped_when_fetched()
	{
		var item = new FeedItemDto
		{
			Id = "1",
			Title = "NPORT-P  - Monthly Portfolio Investments Report on Form N-PORT (Public)",
		};

		Assert.Null(NewsFeedHelpers.ToHeadline(Sec, item, "SPY", CikTickers, Now));
	}

	[Theory]
	[InlineData("SPY NPORT-P: Monthly Portfolio Investments Report on Form N-PORT (Public)", true)]
	[InlineData("SPY 497: Definitive materials", true)]
	[InlineData("Vanguard (VOO) SC 13G: Statement of ownership", true)]
	[InlineData("AAPL 4: Statement of changes in beneficial ownership of securities", false)]
	[InlineData("Shutterstock, Inc. (SSTK) 8-K: Officers / directors, Reg FD", false)]
	[InlineData("ClimateRock 8-K", false)]
	public void A_routine_filing_already_archived_is_recognised_by_its_headline(
		string text,
		bool excluded
	) =>
		Assert.Equal(
			excluded,
			NewsFeedHelpers.IsExcludedSecHeadline(
				new()
				{
					SourceId = "lt",
					ProviderCode = "SEC",
					Headline = text,
				}
			)
		);

	[Fact]
	public void Only_items_within_the_retention_are_kept()
	{
		Assert.True(NewsFeedHelpers.IsWithinRetention(Now.AddDays(-10), Now, 365));
		Assert.False(NewsFeedHelpers.IsWithinRetention(Now.AddDays(-400), Now, 365));
		Assert.True(NewsFeedHelpers.IsWithinRetention(Now.AddYears(-5), Now, 0));
	}

	[Fact]
	public void A_filer_the_sec_has_no_ticker_for_stays_untagged()
	{
		var item = FeedParser.Parse(FeedFixtures.Open("sec-current-8k.xml")).Items[0];

		var headline = NewsFeedHelpers.ToHeadline(Sec, item, null, CikTickers, Now)!;

		Assert.Equal("ClimateRock 8-K: Bylaws / fiscal year, Shareholder vote", headline.Headline);
		Assert.Empty(headline.Symbols);
	}

	[Fact]
	public void A_company_feed_entry_is_tagged_with_the_symbol_it_was_asked_for()
	{
		var item = FeedParser.Parse(FeedFixtures.Open("sec-company.xml")).Items[0];

		var headline = NewsFeedHelpers.ToHeadline(Sec, item, "aapl", CikTickers, Now)!;

		Assert.Equal(
			"AAPL 4: Statement of changes in beneficial ownership of securities",
			headline.Headline
		);
		Assert.Equal(["AAPL"], headline.Symbols);
	}

	[Fact]
	public void A_press_release_is_tagged_with_the_tickers_it_names()
	{
		var items = FeedParser.Parse(FeedFixtures.Open("prnewswire.xml")).Items;
		var prn = NewsFeedHelpers.FindFeed("PRN")!;

		var headlines = items
			.Select(x => NewsFeedHelpers.ToHeadline(prn, x, null, null, Now)!)
			.ToList();

		Assert.Equal("MRP", headlines[0].Symbol);
		Assert.Equal("KMB", headlines[1].Symbol);
	}

	[Fact]
	public void A_feed_that_does_not_tag_keeps_tickers_in_text_untagged()
	{
		var cnbc = NewsFeedHelpers.FindFeed("CNBC")!;
		var item = new FeedItemDto { Id = "1", Title = "Bank (NYSE: BAC) rallies" };

		Assert.Empty(NewsFeedHelpers.ToHeadline(cnbc, item, null, null, Now)!.Symbols);
	}

	[Fact]
	public void An_item_without_time_gets_now_and_one_without_title_is_dropped()
	{
		var fed = NewsFeedHelpers.FindFeed("FED")!;

		Assert.Equal(
			Now,
			NewsFeedHelpers.ToHeadline(fed, new() { Id = "1", Title = "x" }, null, null, Now)!.Time
		);
		Assert.Null(
			NewsFeedHelpers.ToHeadline(fed, new() { Id = "1", Title = " " }, null, null, Now)
		);
	}

	[Fact]
	public void Long_summaries_are_cut()
	{
		var summary = NewsFeedHelpers.TrimSummary(
			new string('x', FeedNewsConstants.MaxSummaryLength + 10)
		)!;

		Assert.Equal(FeedNewsConstants.MaxSummaryLength + 1, summary.Length);
		Assert.Null(NewsFeedHelpers.TrimSummary("  "));
	}

	[Fact]
	public void Symbol_urls_use_the_feed_ticker_and_for_sec_the_padded_cik_and_date()
	{
		Assert.Equal(
			"https://feeds.finance.yahoo.com/rss/2.0/headline?s=BRK-B&region=US&lang=en-US",
			NewsFeedHelpers.BuildSymbolUrl(NewsFeedHelpers.FindFeed("YF")!, "BRK B", null, null)
		);
		Assert.Equal(
			"https://www.sec.gov/cgi-bin/browse-edgar?action=getcompany&CIK=0000320193&type=&dateb=20261020&owner=include&count=40&output=atom",
			NewsFeedHelpers.BuildSymbolUrl(
				Sec,
				"AAPL",
				320193,
				new DateTimeOffset(2026, 10, 20, 15, 0, 0, TimeSpan.Zero)
			)
		);
		Assert.Null(NewsFeedHelpers.BuildSymbolUrl(Sec, "XYZ", null, null));
		Assert.Null(
			NewsFeedHelpers.BuildSymbolUrl(NewsFeedHelpers.FindFeed("FED")!, "AAPL", null, null)
		);
	}

	[Fact]
	public void Providers_say_what_each_feed_can_do()
	{
		var providers = FeedNewsConstants.Feeds.ToDictionary(
			x => x.Code,
			NewsFeedHelpers.ToProvider
		);

		Assert.True(
			providers["SEC"]
				.Capabilities.HasFlag(
					NewsSourceCapabilities.BySymbol | NewsSourceCapabilities.BroadTape
				)
		);
		Assert.True(providers["PRN"].Capabilities.HasFlag(NewsSourceCapabilities.BySymbol));
		Assert.False(providers["CNBC"].Capabilities.HasFlag(NewsSourceCapabilities.BySymbol));
		Assert.False(providers["YF"].Capabilities.HasFlag(NewsSourceCapabilities.BroadTape));
		Assert.Equal(
			["SEC", "PRN", "FED", "BLS"],
			providers.Values.Where(x => x.SelectedByDefault).Select(x => x.Code)
		);
		Assert.Equal(
			["SEC", "PRN", "FED", "BLS"],
			NewsFeedHelpers.GetBackgroundFeeds().Select(x => x.Code)
		);
	}

	[Fact]
	public void A_filing_index_reads_as_its_items_and_documents_with_the_press_release()
	{
		const string url =
			"https://www.sec.gov/Archives/edgar/data/1549346/000114036126041191/0001140361-26-041191-index.htm";

		var (text, pressRelease) = NewsFeedHelpers.ParseSecFilingIndex(FilingIndex, url);

		Assert.Contains("Form 8-K - Current report", text);
		Assert.Contains("Filing Date: 2026-10-29", text);
		Assert.Contains("  Item 5.02: Departure of Directors or Certain Officers", text);
		Assert.Contains("  EX-99.1 — EXHIBIT 99.1", text);
		Assert.Equal(
			"https://www.sec.gov/Archives/edgar/data/1549346/000114036126041191/ef20084541_ex99-1.htm",
			pressRelease
		);
	}

	private static readonly LT.Contracts.Dtos.News.NewsFeedDefinitionDto Sec =
		NewsFeedHelpers.FindFeed(FeedNewsConstants.SecCode)!;

	// The parts of a real EDGAR filing index page (2026-10-29) the article is made of
	private const string FilingIndex = """
		<div id="formName">
		   <strong>Form 8-K</strong> - Current report:
		</div>
		<div class="formGrouping">
		   <div class="infoHead">Filing Date</div>
		   <div class="info">2026-10-29</div>
		   <div class="infoHead">Accepted</div>
		   <div class="info">2026-10-29 17:11:17</div>
		</div>
		<div class="formGrouping">
		   <div class="infoHead">Items</div>
		   <div class="info">Item 5.02: Departure of Directors or Certain Officers; Election of Directors<br />Item 7.01: Regulation FD Disclosure<br />Item 9.01: Financial Statements and Exhibits<br /></div>
		</div>
		<table class="tableFile" summary="Document Format Files">
		   <tr>
		      <th scope="col">Seq</th><th scope="col">Description</th><th scope="col">Document</th><th scope="col">Type</th><th scope="col">Size</th>
		   </tr>
		   <tr>
		      <td scope="row">1</td>
		      <td scope="row">8-K</td>
		      <td scope="row"><a href="/ix?doc=/Archives/edgar/data/1549346/000114036126041191/ef20084541_8k.htm">ef20084541_8k.htm</a></td>
		      <td scope="row">8-K</td>
		      <td scope="row">42334</td>
		   </tr>
		   <tr class="evenRow">
		      <td scope="row">4</td>
		      <td scope="row">EXHIBIT 99.1</td>
		      <td scope="row"><a href="/Archives/edgar/data/1549346/000114036126041191/ef20084541_ex99-1.htm">ef20084541_ex99-1.htm</a></td>
		      <td scope="row">EX-99.1</td>
		      <td scope="row">8136</td>
		   </tr>
		</table>
		""";
}
