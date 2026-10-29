using LT.Contracts.Constants;
using LT.Contracts.Helpers;
using LT.Sec;

namespace LT.Tests.Sec;

public class SecJsonParserTests
{
	[Fact]
	public void Company_tickers_map_upper_case_tickers_to_ciks_first_listing_wins()
	{
		const string json = """
			{"0":{"cik_str":1045810,"ticker":"NVDA","title":"NVIDIA CORP"},
			 "1":{"cik_str":320193,"ticker":"AAPL","title":"Apple Inc."},
			 "2":{"cik_str":1067983,"ticker":"BRK-B","title":"BERKSHIRE HATHAWAY INC"},
			 "3":{"cik_str":999,"ticker":"aapl","title":"Duplicate"},
			 "4":{"ticker":"NOCIK"}}
			""";

		var map = SecJsonParser.ParseCompanyTickers(json);

		Assert.Equal(3, map.Count);
		Assert.Equal(320193, map["AAPL"]);
		Assert.Equal(1067983, map["BRK-B"]);
	}

	[Fact]
	public void Concept_reads_share_values_and_the_newest_us_report_is_picked()
	{
		const string json = """
			{"cik":320193,"taxonomy":"dei","tag":"EntityCommonStockSharesOutstanding",
			 "units":{"shares":[
			  {"end":"2026-04-17","val":14687356000,"accn":"a","fy":2026,"fp":"Q2","form":"10-Q","filed":"2026-05-01"},
			  {"end":"2026-07-17","val":14594180000,"accn":"b","fy":2026,"fp":"Q3","form":"10-Q","filed":"2026-07-31"},
			  {"end":"2026-01-16","val":14681140000,"accn":"c","fy":2026,"fp":"Q1","form":"10-Q","filed":"2026-01-30"}
			 ]}}
			""";

		var facts = SecJsonParser.ParseConcept(json);
		var picked = MarketCapHelpers.PickSharesOutstanding(facts, new DateOnly(2026, 10, 26));

		Assert.Equal(3, facts.Count);
		Assert.NotNull(picked);
		Assert.Equal(14_594_180_000, picked.Shares);
		Assert.Equal(new DateOnly(2026, 7, 17), picked.AsOf);
	}

	[Fact]
	public void Company_facts_combine_the_cover_page_and_balance_sheet_counts()
	{
		const string json = """
			{"cik":1652044,"entityName":"Alphabet Inc.","facts":{
			 "dei":{"EntityPublicFloat":{"units":{"USD":[{"end":"2025-06-30","val":1,"form":"10-K","filed":"2026-02-01"}]}}},
			 "us-gaap":{
			  "CommonStockSharesOutstanding":{"units":{"shares":[
			   {"end":"2026-03-31","val":12116000000,"form":"10-Q","filed":"2026-04-30"},
			   {"end":"2026-06-30","val":12230000000,"form":"10-Q","filed":"2026-07-23"}]}},
			  "Revenues":{"units":{"USD":[{"end":"2026-06-30","val":5,"form":"10-Q","filed":"2026-07-23"}]}}}}}
			""";

		var facts = SecJsonParser.ParseCompanyShareFacts(json);

		Assert.Equal(2, facts.Count);
		Assert.Equal(
			12_230_000_000,
			MarketCapHelpers.PickSharesOutstanding(facts, new DateOnly(2026, 10, 26))?.Shares
		);
	}

	[Fact]
	public void Company_facts_add_the_weighted_averages_with_their_period()
	{
		const string json = """
			{"cik":1326801,"facts":{"us-gaap":{
			  "WeightedAverageNumberOfSharesOutstandingBasic":{"units":{"shares":[
			   {"start":"2026-07-01","end":"2026-09-30","val":2552000000,"form":"10-Q","filed":"2026-10-29"}]}},
			  "WeightedAverageNumberOfDilutedSharesOutstanding":{"units":{"shares":[
			   {"start":"2026-07-01","end":"2026-09-30","val":2568000000,"form":"10-Q","filed":"2026-10-29"}]}}}}}
			""";

		var facts = SecJsonParser.ParseCompanyShareFacts(json);

		Assert.Equal(2, facts.Count);
		Assert.Equal(SecConstants.WeightedAverageBasicConcept, facts[0].Concept);
		Assert.Equal(new DateOnly(2026, 7, 1), facts[0].Start);
		Assert.Null(MarketCapHelpers.PickSharesOutstanding(facts, new DateOnly(2026, 10, 28)));
		Assert.Equal(
			2_552_000_000,
			MarketCapHelpers.PickWeightedAverageShares(facts, new DateOnly(2026, 10, 28))?.Shares
		);
	}

	[Fact]
	public void Company_financial_facts_take_the_asked_concepts_in_every_unit()
	{
		const string json = """
			{"cik":320193,"entityName":"Apple Inc.","facts":{
			  "dei":{"EntityCommonStockSharesOutstanding":{"units":{"shares":[
			   {"end":"2026-07-17","val":14594180000,"form":"10-Q","filed":"2026-07-31"}]}}},
			  "us-gaap":{
			  "NetIncomeLoss":{"units":{"USD":[
			   {"start":"2026-03-29","end":"2026-06-27","val":29789000000,"fy":2026,"fp":"Q3","form":"10-Q","filed":"2026-07-31"}]}},
			  "EarningsPerShareDiluted":{"units":{"USD/shares":[
			   {"start":"2026-03-29","end":"2026-06-27","val":2.02,"form":"10-Q","filed":"2026-07-31"}]}},
			  "Assets":{"units":{"USD":[{"end":"2026-06-27","val":383266000000,"form":"10-Q","filed":"2026-07-31"}]}},
			  "AccountsPayableCurrent":{"units":{"USD":[{"end":"2026-06-27","val":1,"form":"10-Q","filed":"2026-07-31"}]}}}}}
			""";

		var facts = SecJsonParser.ParseCompanyFinancialFacts(
			json,
			[
				"NetIncomeLoss",
				"EarningsPerShareDiluted",
				"Assets",
				"EntityCommonStockSharesOutstanding",
			]
		);

		Assert.Equal("Apple Inc.", facts.EntityName);
		Assert.Equal(
			["NetIncomeLoss", "EarningsPerShareDiluted", "Assets"],
			facts.Facts.Select(x => x.Concept)
		);
		Assert.Equal(new DateOnly(2026, 3, 29), facts.Facts[0].Start);
		Assert.Equal(2.02m, facts.Facts[1].Value);
		Assert.Null(facts.Facts[2].Start);
		Assert.Empty(SecJsonParser.ParseCompanyFinancialFacts("""{"cik":1}""", ["Assets"]).Facts);
	}

	[Fact]
	public void Submissions_give_the_newest_inline_us_report()
	{
		const string json = """
			{"cik":"1067983","filings":{"recent":{
			  "accessionNumber":["0001193125-26-400000","0001193125-26-341032","0001193125-26-200000","0000000000-26-000001"],
			  "filingDate":["2026-09-01","2026-08-10","2026-05-04","2026-08-20"],
			  "form":["8-K","10-Q","10-Q","10-Q"],
			  "primaryDocument":["d8k.htm","brka-20260630.htm","brka-20260331.htm","old.txt"],
			  "isInlineXBRL":[1,1,1,0]}}}
			""";

		var filing = SecJsonParser.ParseLatestInlineFiling(
			json,
			SecConstants.SharesOutstandingForms
		);

		Assert.Equal(
			new SecFilingRef(
				"0001193125-26-341032",
				"brka-20260630.htm",
				"10-Q",
				new DateOnly(2026, 8, 10)
			),
			filing
		);
		Assert.Null(
			SecJsonParser.ParseLatestInlineFiling(
				"""{"filings":{"recent":{"accessionNumber":["a"],"filingDate":["2026-01-01"],"form":["20-F"],"primaryDocument":["x.htm"],"isInlineXBRL":[1]}}}""",
				SecConstants.SharesOutstandingForms
			)
		);
		Assert.Null(
			SecJsonParser.ParseLatestInlineFiling("{}", SecConstants.SharesOutstandingForms)
		);
	}

	[Fact]
	public void Missing_units_or_malformed_entries_are_skipped()
	{
		const string json = """
			{"units":{"shares":[
			  {"end":"not a date","val":1,"form":"10-Q","filed":"2026-01-01"},
			  {"end":"2026-01-01","form":"10-Q","filed":"2026-01-01"},
			  {"end":"2026-01-01","val":7,"form":"10-Q","filed":"2026-01-02"}]}}
			""";

		var facts = SecJsonParser.ParseConcept(json);

		Assert.Equal(7, Assert.Single(facts).Value);
		Assert.Empty(SecJsonParser.ParseConcept("""{"units":{"USD":[]}}"""));
		Assert.Empty(SecJsonParser.ParseCompanyShareFacts("""{"cik":1}"""));
	}
}
