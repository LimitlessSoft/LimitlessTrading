using LT.Sec;

namespace LT.Tests.Sec;

public class SecAtomParserTests
{
	[Fact]
	public void Reads_who_filed_which_items_and_when()
	{
		var filings = SecAtomParser.ParseCurrentFilings(SecFixtures.CurrentFilings);

		Assert.Equal([1736243, 50863, 1018724], filings.Select(x => x.Cik));
		var amazon = filings[2];
		Assert.Equal("8-K", amazon.Form);
		Assert.Equal("AMAZON COM INC", amazon.Company);
		Assert.Equal("0001018724-26-000038", amazon.AccessionNumber);
		Assert.Equal(["2.02", "9.01"], amazon.Items);
		Assert.Equal(
			new DateTimeOffset(2026, 10, 29, 16, 5, 27, TimeSpan.FromHours(-4)),
			amazon.AcceptedAt
		);
		Assert.Equal(
			"https://www.sec.gov/Archives/edgar/data/1018724/000101872426000038/0001018724-26-000038-index.htm",
			amazon.Url
		);
		Assert.Equal(["5.03", "9.01"], filings[0].Items);
	}

	[Fact]
	public void Something_other_than_the_feed_throws()
	{
		Assert.Throws<FormatException>(() =>
			SecAtomParser.ParseCurrentFilings("<html><body>Too many requests</body></html>")
		);
		Assert.Throws<FormatException>(() => SecAtomParser.ParseCurrentFilings("not xml"));
	}
}
