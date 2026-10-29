using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

/// <summary>
/// The Overview's market state and today's hours from IB's trading / liquid hours, in the
/// exchange's zone. Thursday 29 Oct 2026 and Friday 30 Oct; New York is UTC-4 then.
/// </summary>
public class OverviewHelpersTests
{
	private const string Zone = "US/Eastern";

	// As IB lists a US stock: extended 04:00–20:00, regular 09:30–16:00, weekend closed
	private const string Trading =
		"20261029:0400-20261029:2000;20261030:0400-20261030:2000;20261031:CLOSED;"
		+ "20261101:CLOSED;20261102:0400-20261102:2000";
	private const string Liquid =
		"20261029:0930-20261029:1600;20261030:0930-20261030:1600;20261031:CLOSED;"
		+ "20261101:CLOSED;20261102:0930-20261102:1600";

	private static DateTimeOffset NewYork(int day, int hour, int minute = 0) =>
		new(2026, 10, day, hour, minute, 0, TimeSpan.FromHours(-4));

	[Theory]
	[InlineData(29, 3, 0, "Closed · opens 04:00")]
	[InlineData(29, 7, 15, "Pre-market · opens 09:30")]
	[InlineData(29, 9, 30, "Open · closes 16:00")]
	[InlineData(29, 15, 59, "Open · closes 16:00")]
	[InlineData(29, 16, 0, "After hours · until 20:00")]
	[InlineData(29, 21, 0, "Closed · opens Fri 04:00")]
	[InlineData(30, 20, 30, "Closed · opens Mon 04:00")]
	public void The_market_state_follows_the_sessions(
		int day,
		int hour,
		int minute,
		string state
	) =>
		Assert.Equal(
			state,
			OverviewHelpers.DescribeMarketState(Trading, Liquid, Zone, NewYork(day, hour, minute))
		);

	[Fact]
	public void Past_the_last_listed_session_the_market_is_just_closed() =>
		Assert.Equal(
			"Closed",
			OverviewHelpers.DescribeMarketState(
				Trading,
				Liquid,
				Zone,
				// Monday 2 Nov, after the clocks went back (UTC-5)
				new DateTimeOffset(2026, 11, 2, 21, 0, 0, TimeSpan.FromHours(-5))
			)
		);

	[Fact]
	public void Sessions_that_touch_are_one_so_a_market_open_around_the_clock_closes_at_its_end() =>
		Assert.Equal(
			"Open · closes Sat 00:00",
			OverviewHelpers.DescribeMarketState(
				"",
				"20261029:0000-20261030:0000;20261030:0000-20261031:0000",
				Zone,
				NewYork(29, 12)
			)
		);

	[Fact]
	public void No_hours_or_an_unknown_zone_says_nothing()
	{
		Assert.Null(OverviewHelpers.DescribeMarketState("", "", Zone, NewYork(29, 12)));
		Assert.Null(
			OverviewHelpers.DescribeMarketState(Trading, Liquid, "Mars/Base", NewYork(29, 12))
		);
	}

	[Fact]
	public void Today_is_the_exchange_day_listed()
	{
		Assert.Equal("09:30–16:00", OverviewHelpers.DescribeToday(Liquid, Zone, NewYork(29, 12)));
		Assert.Equal("04:00–20:00", OverviewHelpers.DescribeToday(Trading, Zone, NewYork(29, 12)));
		Assert.Equal("Closed today", OverviewHelpers.DescribeToday(Liquid, Zone, NewYork(31, 12)));
		// 01:00 UTC on the 30th is still the 29th in New York
		Assert.Equal(
			"09:30–16:00",
			OverviewHelpers.DescribeToday(
				Liquid,
				Zone,
				new DateTimeOffset(2026, 10, 30, 1, 0, 0, TimeSpan.Zero)
			)
		);
	}

	[Fact]
	public void A_session_running_into_the_next_day_is_marked_and_an_unlisted_day_says_nothing()
	{
		// A future's evening session
		const string hours = "20261029:1800-20261030:1700";

		Assert.Equal("18:00–17:00 +1", OverviewHelpers.DescribeToday(hours, Zone, NewYork(29, 19)));
		Assert.Null(OverviewHelpers.DescribeToday(hours, Zone, NewYork(30, 12)));
	}

	[Fact]
	public void Valid_exchanges_drop_blanks_and_repeats() =>
		Assert.Equal(
			["SMART", "ARCA", "NYSE"],
			OverviewHelpers.GetValidExchanges("SMART, ARCA,,NYSE,arca")
		);

	[Theory]
	[InlineData("STK", "Stock")]
	[InlineData(" ind ", "Index")]
	[InlineData("CASH", "Forex")]
	[InlineData("CRYPTO", "Crypto")]
	[InlineData("IOPT", "IOPT")]
	[InlineData("", "")]
	public void Security_types_read_in_words(string type, string name) =>
		Assert.Equal(name, OverviewHelpers.GetSecurityTypeName(type));

	[Theory]
	[InlineData(0.01, "0.01")]
	[InlineData(0.00005, "0.00005")]
	[InlineData(5, "5")]
	[InlineData(0, "")]
	public void The_min_tick_has_no_trailing_zeros(double tick, string text) =>
		Assert.Equal(text, OverviewHelpers.FormatMinTick(tick));

	[Theory]
	[InlineData("Meaning", "Detail", "Meaning\n\nDetail")]
	[InlineData("Meaning", null, "Meaning")]
	[InlineData(null, "Detail", "Detail")]
	[InlineData(" ", null, null)]
	public void A_tip_puts_the_meaning_above_the_details(
		string? description,
		string? details,
		string? tip
	) => Assert.Equal(tip, OverviewHelpers.JoinTip(description, details));

	[Theory]
	[InlineData("ISIN", true)]
	[InlineData(" cusip ", true)]
	[InlineData("FIGI", true)]
	[InlineData("XYZ", false)]
	[InlineData(null, false)]
	public void Known_security_ids_are_explained(string? type, bool explained) =>
		Assert.Equal(explained, OverviewHelpers.DescribeSecurityId(type) != null);
}
