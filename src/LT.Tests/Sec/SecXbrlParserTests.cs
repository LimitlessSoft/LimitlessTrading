using LT.Contracts.Helpers;
using LT.Sec;

namespace LT.Tests.Sec;

public class SecXbrlParserTests
{
	// Shaped like brka-20260630_htm.xml: per-class cover-page counts, plus contexts to skip
	private const string Instance = """
		<?xml version="1.0" encoding="utf-8"?>
		<xbrl xmlns="http://www.xbrl.org/2003/instance"
		      xmlns:xbrldi="http://xbrl.org/2006/xbrldi"
		      xmlns:dei="http://xbrl.sec.gov/dei/2025"
		      xmlns:us-gaap="http://fasb.org/us-gaap/2025">
		  <context id="c-1">
		    <entity><identifier scheme="http://www.sec.gov/CIK">0001067983</identifier></entity>
		    <period><startDate>2026-01-01</startDate><endDate>2026-06-30</endDate></period>
		  </context>
		  <context id="c-a">
		    <entity><identifier scheme="http://www.sec.gov/CIK">0001067983</identifier>
		      <segment><xbrldi:explicitMember dimension="us-gaap:StatementClassOfStockAxis">us-gaap:CommonClassAMember</xbrldi:explicitMember></segment>
		    </entity>
		    <period><instant>2026-07-29</instant></period>
		  </context>
		  <context id="c-b">
		    <entity><identifier scheme="http://www.sec.gov/CIK">0001067983</identifier>
		      <segment><xbrldi:explicitMember dimension="us-gaap:StatementClassOfStockAxis">us-gaap:CommonClassBMember</xbrldi:explicitMember></segment>
		    </entity>
		    <period><instant>2026-07-29</instant></period>
		  </context>
		  <context id="c-lp">
		    <entity><identifier scheme="http://www.sec.gov/CIK">0001067983</identifier>
		      <segment><xbrldi:explicitMember dimension="dei:LegalEntityAxis">brka:SubsidiaryMember</xbrldi:explicitMember></segment>
		    </entity>
		    <period><instant>2026-07-29</instant></period>
		  </context>
		  <dei:DocumentType contextRef="c-1">10-Q</dei:DocumentType>
		  <dei:EntityCommonStockSharesOutstanding contextRef="c-a" decimals="INF" unitRef="shares">488450</dei:EntityCommonStockSharesOutstanding>
		  <dei:EntityCommonStockSharesOutstanding contextRef="c-b" decimals="INF" unitRef="shares">1408035161</dei:EntityCommonStockSharesOutstanding>
		  <dei:EntityCommonStockSharesOutstanding contextRef="c-lp" decimals="INF" unitRef="shares">100</dei:EntityCommonStockSharesOutstanding>
		  <us-gaap:CommonStockSharesOutstanding contextRef="c-a" decimals="INF" unitRef="shares">501101</us-gaap:CommonStockSharesOutstanding>
		</xbrl>
		""";

	[Fact]
	public void Cover_page_counts_are_read_per_share_class()
	{
		var facts = SecXbrlParser.ParseCoverPageShares(
			Instance.Trim(),
			"10-Q",
			new DateOnly(2026, 8, 10)
		);

		Assert.Equal(2, facts.Count);
		Assert.Equal("CommonClassAMember", facts[0].ShareClass);
		Assert.Equal(488_450, facts[0].Value);
		Assert.Equal("CommonClassBMember", facts[1].ShareClass);
		Assert.All(
			facts,
			x =>
			{
				Assert.Equal(new DateOnly(2026, 7, 29), x.End);
				Assert.Equal(new DateOnly(2026, 8, 10), x.Filed);
				Assert.Equal("10-Q", x.Form);
				Assert.Null(x.Start);
			}
		);
		Assert.Equal(
			2_140_710_161,
			MarketCapHelpers.PickFilingShares(facts, "BRK-B", new DateOnly(2026, 10, 28))?.Shares
		);
	}

	[Fact]
	public void An_undivided_count_has_no_class()
	{
		const string xml = """
			<xbrl xmlns="http://www.xbrl.org/2003/instance" xmlns:dei="http://xbrl.sec.gov/dei/2024">
			  <context id="c"><entity><identifier scheme="x">1</identifier></entity><period><instant>2026-07-17</instant></period></context>
			  <dei:EntityCommonStockSharesOutstanding contextRef="c" unitRef="shares">14594180000</dei:EntityCommonStockSharesOutstanding>
			</xbrl>
			""";

		var fact = Assert.Single(SecXbrlParser.ParseCoverPageShares(xml, "10-K", default));

		Assert.Null(fact.ShareClass);
		Assert.Equal(14_594_180_000, fact.Value);
	}
}
