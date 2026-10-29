using System.Text;
using System.Xml;
using LT.Feeds;

namespace LT.Tests.Feeds;

public class FeedParserTests
{
	[Fact]
	public void Edgar_current_filings_are_read_with_their_iso_8859_1_declaration()
	{
		using var stream = FeedFixtures.Open("sec-current-8k.xml");

		var feed = FeedParser.Parse(stream);

		Assert.Equal(3, feed.Items.Count);
		var first = feed.Items[0];
		Assert.Equal("8-K - ClimateRock (0001903392) (Filer)", first.Title);
		Assert.Equal("urn:tag:sec.gov,2008:accession-number=0001213900-26-113900", first.Id);
		Assert.Equal(
			"https://www.sec.gov/Archives/edgar/data/1903392/000121390026113900/0001213900-26-113900-index.htm",
			first.Url
		);
		Assert.Equal(DateTimeOffset.Parse("2026-10-29T21:11:26Z"), first.Time);
		Assert.Contains("Item 5.03", first.Summary);
		Assert.Equal(["8-K"], first.Categories);
	}

	[Fact]
	public void A_latin_1_feed_keeps_its_accents()
	{
		var xml =
			"<?xml version=\"1.0\" encoding=\"ISO-8859-1\" ?><feed xmlns=\"http://www.w3.org/2005/Atom\">"
			+ "<entry><title>8-K - Société Générale (0000000001) (Filer)</title>"
			+ "<id>urn:1</id><updated>2026-10-29T17:00:00-04:00</updated></entry></feed>";

		var feed = FeedParser.Parse(new MemoryStream(Encoding.Latin1.GetBytes(xml)));

		Assert.Equal("8-K - Société Générale (0000000001) (Filer)", feed.Items[0].Title);
	}

	[Fact]
	public void An_edgar_company_feed_names_the_company_and_links_each_filing()
	{
		using var stream = FeedFixtures.Open("sec-company.xml");

		var feed = FeedParser.Parse(stream);

		Assert.Equal("Apple Inc.", feed.Title);
		Assert.Equal(2, feed.Items.Count);
		Assert.Equal(
			"4  - Statement of changes in beneficial ownership of securities",
			feed.Items[0].Title
		);
		Assert.EndsWith("0001140361-26-040540-index.htm", feed.Items[0].Url);
		Assert.Equal(DateTimeOffset.Parse("2026-10-22T22:32:20Z"), feed.Items[0].Time);
	}

	[Theory]
	[InlineData("prnewswire.xml", 3, "2026-10-29T21:10:00Z")]
	[InlineData("federalreserve.xml", 2, "2026-10-29T20:30:00Z")]
	[InlineData("bls-cpi.xml", 2, "2026-10-14T11:50:31.2Z")]
	[InlineData("fda.xml", 2, "2026-10-22T16:58:19Z")]
	[InlineData("cnbc.xml", 2, "2026-10-29T21:03:17Z")]
	[InlineData("marketwatch.xml", 2, "2026-10-29T21:07:00Z")]
	[InlineData("yahoo-aapl.xml", 2, "2026-10-29T16:58:00Z")]
	[InlineData("seekingalpha-aapl.xml", 1, "2026-10-29T13:28:42Z")]
	[InlineData("businesswire-earnings.xml", 2, "2026-10-29T21:00:00Z")]
	public void Every_feed_gives_its_items_with_a_time(string file, int count, string firstTime)
	{
		using var stream = FeedFixtures.Open(file);

		var feed = FeedParser.Parse(stream);

		Assert.Equal(count, feed.Items.Count);
		Assert.Equal(DateTimeOffset.Parse(firstTime), feed.Items[0].Time);
		Assert.All(
			feed.Items,
			x =>
			{
				Assert.False(string.IsNullOrWhiteSpace(x.Id));
				Assert.False(string.IsNullOrWhiteSpace(x.Title));
				Assert.StartsWith("http", x.Url);
			}
		);
	}

	[Fact]
	public void The_feds_cdata_links_and_dates_are_read()
	{
		using var stream = FeedFixtures.Open("federalreserve.xml");

		var item = FeedParser.Parse(stream).Items[1];

		Assert.Equal("Federal Reserve issues FOMC statement", item.Title);
		Assert.Equal(
			"https://www.federalreserve.gov/newsevents/pressreleases/monetary20261028a.htm",
			item.Url
		);
		Assert.Equal(["Monetary Policy"], item.Categories);
	}

	[Fact]
	public void Seeking_alpha_tags_its_items_with_tickers()
	{
		using var stream = FeedFixtures.Open("seekingalpha-aapl.xml");

		var item = FeedParser.Parse(stream).Items[0];

		Assert.Contains("AAPL", item.Symbols);
		Assert.Contains("GOOGL", item.Symbols);
	}

	[Theory]
	[InlineData("Thu, 29 Oct 2026 21:03:17 GMT", "2026-10-29T21:03:17Z")]
	[InlineData("Thu, 22 Oct 2026 12:58:19 EDT", "2026-10-22T16:58:19Z")]
	[InlineData("Tue, 03 Feb 2026 08:30:00 EST", "2026-02-03T13:30:00Z")]
	[InlineData("Thu, 29 Oct 2026 20:54 GMT", "2026-10-29T20:54:00Z")]
	[InlineData("Thu, 29 Oct 2026 21:00:00 UT", "2026-10-29T21:00:00Z")]
	[InlineData("Thu, 29 Oct 2026 09:28:42 -0400", "2026-10-29T13:28:42Z")]
	[InlineData("29 Oct 2026 21:10:00 +0000", "2026-10-29T21:10:00Z")]
	[InlineData("2026-10-29T17:11:26-04:00", "2026-10-29T21:11:26Z")]
	public void Dates_are_read_with_zone_names_and_offsets(string text, string expected) =>
		Assert.Equal(DateTimeOffset.Parse(expected), FeedParser.ParseDate(text));

	[Theory]
	[InlineData("")]
	[InlineData("yesterday")]
	[InlineData("Thu, 29 Foo 2026 21:03:17 GMT")]
	[InlineData("Thu, 29 Oct 2026 21:03:17 XYZ")]
	public void Unreadable_dates_are_null(string text) => Assert.Null(FeedParser.ParseDate(text));

	[Fact]
	public void A_page_that_is_not_a_feed_is_refused() =>
		Assert.Throws<XmlException>(() =>
			FeedParser.Parse("<html><body>Access Denied</body></html>")
		);
}
