using LT.Contracts.Constants;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

/// <summary>Reading plain symbol lists and naming watchlist files.</summary>
public class WatchlistFileHelpersTests
{
	[Fact]
	public void One_symbol_per_line()
	{
		Assert.Equal(
			["AAPL", "MSFT", "BRK B"],
			WatchlistFileHelpers.ParseSymbols("aapl\r\n\r\n  MSFT \nBRK B\n")
		);
	}

	[Fact]
	public void Comma_semicolon_and_tab_separated_with_quotes()
	{
		Assert.Equal(
			["SPY", "QQQ", "IWM", "DIA", "TLT"],
			WatchlistFileHelpers.ParseSymbols("\"SPY\", 'QQQ',IWM\nDIA;TLT\t,")
		);
	}

	[Fact]
	public void A_header_comments_duplicates_and_a_byte_order_mark_are_dropped()
	{
		Assert.Equal(
			["NVDA", "AMD"],
			WatchlistFileHelpers.ParseSymbols("﻿Symbol\n# semis\nNVDA\nnvda\nAMD\n")
		);
	}

	[Fact]
	public void Header_word_is_kept_when_not_first()
	{
		Assert.Equal(["SPY", "TICKER"], WatchlistFileHelpers.ParseSymbols("SPY,ticker"));
	}

	[Theory]
	[InlineData("AAPL", true)]
	[InlineData("BRK.B", true)]
	[InlineData("BRK B", true)]
	[InlineData("EUR.USD", true)]
	[InlineData("M&M", true)]
	[InlineData(".X", false)]
	[InlineData("A{B}", false)]
	[InlineData("ÄPFEL", false)]
	[InlineData("", false)]
	public void Symbols_look_like_tickers(string symbol, bool valid) =>
		Assert.Equal(valid, WatchlistFileHelpers.IsValidSymbol(symbol));

	[Fact]
	public void A_symbol_longer_than_the_limit_is_not_one() =>
		Assert.False(
			WatchlistFileHelpers.IsValidSymbol(
				new string('A', MarketDataConstants.SymbolMaxLength + 1)
			)
		);

	[Theory]
	[InlineData("{ \"format\": 1 }", true)]
	[InlineData("﻿  \n{", true)]
	[InlineData("AAPL,MSFT", false)]
	public void Json_is_told_from_a_symbol_list(string content, bool json) =>
		Assert.Equal(json, WatchlistFileHelpers.IsJson(content));

	[Theory]
	[InlineData("Tech", "ignored.json", "Tech")]
	[InlineData(null, "/home/me/semis.csv", "semis")]
	[InlineData("  ", ".txt", "Imported")]
	[InlineData(null, null, "Imported")]
	public void Import_name_comes_from_the_file(
		string? listName,
		string? fileName,
		string expected
	) => Assert.Equal(expected, WatchlistFileHelpers.GetImportName(listName, fileName));

	[Fact]
	public void Import_name_is_cut_to_the_longest_watchlist_name() =>
		Assert.Equal(
			MarketDataConstants.WatchlistNameMaxLength,
			WatchlistFileHelpers.GetImportName(new string('x', 100), null).Length
		);

	[Fact]
	public void Export_file_name_drops_characters_file_systems_refuse() =>
		Assert.Equal("US_Tech.json", WatchlistFileHelpers.GetFileName("US/Tech"));
}
