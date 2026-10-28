using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class MarketDataStreamHelpersTests
{
	[Theory]
	[InlineData(MarketDataStreamKind.Quote, "", "Quote")]
	[InlineData(MarketDataStreamKind.Quote, "106", "Quote + option IV")]
	[InlineData(MarketDataStreamKind.Quote, "233", "Quote + ticks 233")]
	[InlineData(MarketDataStreamKind.News, "", "News")]
	public void Kind_text_names_the_generic_ticks(
		MarketDataStreamKind kind,
		string genericTicks,
		string expected
	) =>
		Assert.Equal(
			expected,
			MarketDataStreamHelpers.GetKindText(
				new MarketDataStreamDto { Kind = kind, GenericTicks = genericTicks }
			)
		);

	[Fact]
	public void Contract_text_shows_an_option_with_its_strike_right_and_expiry()
	{
		var option = new ContractDto
		{
			Symbol = "AAPL",
			SecurityType = MarketDataConstants.OptionType,
			LastTradeDateOrContractMonth = "20261120",
			Strike = 150,
			Right = MarketDataConstants.CallRight,
		};

		Assert.Equal(
			"AAPL 20261120 150 C · OPT",
			MarketDataStreamHelpers.GetContractText(new MarketDataStreamDto { Contract = option })
		);
		Assert.Equal(
			"#42",
			MarketDataStreamHelpers.GetContractText(
				new MarketDataStreamDto { Contract = new ContractDto { ConId = 42 } }
			)
		);
	}

	[Fact]
	public void Owner_summary_counts_lines_per_owner_most_first()
	{
		MarketDataStreamDto Of(string owner) => new() { Owner = owner };

		Assert.Equal(
			"Watchlist · US 3, Chart 1, Other 1",
			MarketDataStreamHelpers.GetOwnerSummary([
				Of("Chart"),
				Of("Watchlist · US"),
				Of(""),
				Of("Watchlist · US"),
				Of("Watchlist · US"),
			])
		);
		Assert.Equal(string.Empty, MarketDataStreamHelpers.GetOwnerSummary([]));
	}

	[Fact]
	public void A_shared_line_counts_for_each_owner_and_lists_them_all()
	{
		var shared = MarketDataShareHelpers.WithSubscribers(
			new MarketDataStreamDto { Id = 7, Owner = "Watchlist · US" },
			["Watchlist · US", "Chart", "Watchlist · US", ""]
		);

		Assert.Equal(7, shared.Id);
		Assert.Equal(["Watchlist · US", "Chart"], shared.Owners);
		Assert.Equal("Watchlist · US, Chart", shared.Owner);
		Assert.Equal(4, shared.Subscribers);
		Assert.Equal("Watchlist · US, Chart (4)", MarketDataStreamHelpers.GetOwnerText(shared));
		Assert.Equal(
			"Watchlist · US 2, Chart 1",
			MarketDataStreamHelpers.GetOwnerSummary([
				shared,
				new MarketDataStreamDto { Owner = "Watchlist · US", Owners = ["Watchlist · US"] },
			])
		);
		Assert.Equal("—", MarketDataStreamHelpers.GetOwnerText(new MarketDataStreamDto()));
	}

	[Theory]
	[InlineData(-5, "0s")]
	[InlineData(42, "42s")]
	[InlineData(303, "5m 03s")]
	[InlineData(7620, "2h 07m")]
	public void Age_is_short(int seconds, string expected) =>
		Assert.Equal(expected, MarketDataStreamHelpers.FormatAge(TimeSpan.FromSeconds(seconds)));
}
