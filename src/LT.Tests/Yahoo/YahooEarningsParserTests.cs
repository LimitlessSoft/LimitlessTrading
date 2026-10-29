using LT.Contracts.Exceptions;
using LT.Tests.Fakes;
using LT.Yahoo;

namespace LT.Tests.Yahoo;

public class YahooEarningsParserTests
{
	private static readonly DateOnly Thursday = new(2026, 10, 29);

	public YahooEarningsParserTests() => LSCoreContainer.EnsureInitialized();

	[Fact]
	public void Reads_the_reported_eps_and_surprise_of_the_days_rows()
	{
		var page = YahooEarningsParser.Parse(YahooFixtures.Earnings, Thursday);

		Assert.Equal(
			["AMZN", "LLY", "MA", "MRK", "CAT", "LIN", "KYMR"],
			page.Rows.Select(x => x.Symbol)
		);
		var lilly = page.Rows.Single(x => x.Symbol == "LLY");
		Assert.Equal(7.48m, lilly.EpsActual);
		Assert.Equal(17.88m, lilly.EpsSurprisePercent);
		Assert.Equal(Thursday, lilly.Date);
		Assert.Equal("Yahoo Finance", lilly.Source);
		Assert.Equal(-12.27m, page.Rows.Single(x => x.Symbol == "LIN").EpsSurprisePercent);
		Assert.Equal(-1.32m, page.Rows.Single(x => x.Symbol == "KYMR").EpsActual);
		Assert.Equal(7.2m, page.Rows.Single(x => x.Symbol == "CAT").EpsActual);
	}

	[Fact]
	public void A_report_not_out_yet_has_no_figures()
	{
		var amazon = YahooEarningsParser
			.Parse(YahooFixtures.Earnings, Thursday)
			.Rows.Single(x => x.Symbol == "AMZN");

		Assert.Null(amazon.EpsActual);
		Assert.Null(amazon.EpsSurprisePercent);
		Assert.False(amazon.HasFigures);
	}

	[Fact]
	public void Reads_where_the_page_sits_in_the_days_list()
	{
		var page = YahooEarningsParser.Parse(YahooFixtures.Earnings, Thursday);

		Assert.Equal(1, page.First);
		Assert.Equal(298, page.Total);
	}

	[Fact]
	public void A_page_without_a_table_is_a_day_without_earnings()
	{
		var page = YahooEarningsParser.Parse(YahooFixtures.NoEarnings, Thursday);

		Assert.Empty(page.Rows);
		Assert.Equal(0, page.Total);
	}

	[Fact]
	public void Rows_whose_cells_it_cant_find_throw_rather_than_read_as_no_results()
	{
		var changed = YahooFixtures.Earnings.Replace("data-testid-cell=", "data-cell=");

		var ex = Assert.Throws<YahooServiceException>(() =>
			YahooEarningsParser.Parse(changed, Thursday)
		);

		Assert.Contains("layout", ex.Message);
	}
}
