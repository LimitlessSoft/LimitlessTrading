using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Options;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class OptionChainHelpersTests
{
	private static ContractDto Option(double strike, string right) =>
		new()
		{
			SecurityType = "OPT",
			LastTradeDateOrContractMonth = "20261120",
			Strike = strike,
			Right = right,
		};

	private static OptionChainParametersDto Parameters(
		string exchange,
		string tradingClass,
		params string[] expirations
	) =>
		new()
		{
			Exchange = exchange,
			TradingClass = tradingClass,
			Expirations = expirations,
		};

	[Fact]
	public void Expirations_skip_adjusted_classes_and_other_exchanges()
	{
		var expirations = OptionChainHelpers.SelectExpirations(
			"SPY",
			[
				Parameters("SMART", "SPY", "20261026", "20261027"),
				Parameters("SMART", "2SPY", "20261120"),
				Parameters("CBOE", "SPY", "20261028"),
			]
		);

		Assert.Equal(["20261026", "20261027"], expirations.Select(x => x.Expiration));
	}

	[Fact]
	public void Weeklies_join_the_monthlies_and_shared_dates_keep_the_own_class()
	{
		var expirations = OptionChainHelpers.SelectExpirations(
			"SPX",
			[
				Parameters("SMART", "SPXW", "20261026", "20261119"),
				Parameters("SMART", "SPX", "20261119"),
				Parameters("SMART", "SPXO", "20261119"),
			]
		);

		Assert.Equal(
			[("20261026", "SPXW"), ("20261119", "SPX")],
			expirations.Select(x => (x.Expiration, x.TradingClass))
		);
	}

	[Fact]
	public void Without_an_own_class_every_smart_entry_counts()
	{
		var expirations = OptionChainHelpers.SelectExpirations(
			"BRK B",
			[Parameters("SMART", "BRKB", "20261120")]
		);

		Assert.Equal(["20261120"], expirations.Select(x => x.Expiration));
	}

	[Fact]
	public void Calls_and_puts_are_paired_by_strike()
	{
		var strikes = OptionChainHelpers.BuildStrikes([
			Option(110, "P"),
			Option(100, "C"),
			Option(100, "P"),
			Option(110, "C"),
		]);

		Assert.Equal([100d, 110d], strikes.Select(x => x.Strike));
		Assert.All(strikes, x => Assert.True(x.Call != null && x.Put != null));
	}

	[Fact]
	public void Around_price_centres_on_the_nearest_strike()
	{
		var strikes = OptionChainHelpers.BuildStrikes(
			Enumerable.Range(0, 20).Select(i => Option(100 + i * 5, "C"))
		);

		var around = OptionChainHelpers.AroundPrice(strikes, 151, 2);

		Assert.Equal([140d, 145d, 150d, 155d, 160d], around.Select(x => x.Strike));
	}

	[Fact]
	public void Around_price_is_clamped_at_the_edges()
	{
		var strikes = OptionChainHelpers.BuildStrikes(
			Enumerable.Range(0, 5).Select(i => Option(100 + i, "C"))
		);

		Assert.Equal(3, OptionChainHelpers.AroundPrice(strikes, 0, 2).Count);
	}

	[Theory]
	[InlineData("202611", true)]
	[InlineData("20261120", true)]
	[InlineData("2026-11", false)]
	public void Expiry_format(string expiry, bool valid) =>
		Assert.Equal(valid, OptionChainHelpers.ExpiryRegex().IsMatch(expiry));
}
