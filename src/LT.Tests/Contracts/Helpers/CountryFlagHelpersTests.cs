using LT.App.Controls;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

/// <summary>Country names (as Nasdaq writes them), currencies and codes find their flag.</summary>
public class CountryFlagHelpersTests
{
	[Theory]
	[InlineData("United States", "US")]
	[InlineData("USD", "US")]
	[InlineData("us", "US")]
	[InlineData("Euro Zone", "EU")]
	[InlineData("EUR", "EU")]
	[InlineData(" Japan ", "JP")]
	[InlineData("New Zealand", "NZ")]
	[InlineData("United Kingdom", "GB")]
	[InlineData("GBP", "GB")]
	[InlineData("CHF", "CH")]
	[InlineData("South Korea", "KR")]
	[InlineData("germany", "DE")]
	public void Names_currencies_and_codes_map_to_a_flag(string text, string code)
	{
		Assert.Equal(code, CountryFlagHelpers.GetCode(text));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("Atlantis")]
	[InlineData("Earnings")]
	public void Anything_else_has_no_flag(string? text)
	{
		Assert.Null(CountryFlagHelpers.GetCode(text));
	}

	[Fact]
	public void Every_code_has_a_drawn_flag()
	{
		Assert.All(CountryFlagHelpers.Codes, x => Assert.True(CountryFlag.CanDraw(x), x));
	}
}
