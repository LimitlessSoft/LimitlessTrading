using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

/// <summary>Picking the listing a contract known only by its fields means, or none when ambiguous.</summary>
public class ContractPickMatchTests
{
	[Fact]
	public void Nothing_found_is_no_match() =>
		Assert.Null(ContractHelpers.PickMatch(new ContractDto { Symbol = "AAPL" }, []));

	[Fact]
	public void A_single_listing_is_picked()
	{
		var match = ContractHelpers.PickMatch(
			new ContractDto { Symbol = "AAPL" },
			Found(Stock(265598, "USD", "SMART", "NASDAQ"))
		);

		Assert.Equal(265598, match?.ConId);
	}

	[Fact]
	public void With_no_currency_the_us_dollar_listing_is_preferred()
	{
		var match = ContractHelpers.PickMatch(
			new ContractDto { Symbol = "SPY" },
			Found(Stock(237937002, "AUD", "SMART", "ASX"), Stock(756733, "USD", "SMART", "ARCA"))
		);

		Assert.Equal(756733, match?.ConId);
	}

	[Fact]
	public void A_given_currency_must_match()
	{
		var found = Found(
			Stock(237937002, "AUD", "SMART", "ASX"),
			Stock(756733, "USD", "SMART", "ARCA")
		);

		Assert.Equal(
			237937002,
			ContractHelpers
				.PickMatch(new ContractDto { Symbol = "SPY", Currency = "aud" }, found)
				?.ConId
		);
		Assert.Null(
			ContractHelpers.PickMatch(new ContractDto { Symbol = "SPY", Currency = "EUR" }, found)
		);
	}

	[Fact]
	public void The_given_exchange_steers_between_listings_by_primary_exchange()
	{
		var found = Found(Stock(1, "USD", "SMART", "NYSE"), Stock(2, "USD", "SMART", "PINK"));

		Assert.Equal(
			2,
			ContractHelpers
				.PickMatch(new ContractDto { Symbol = "X", Exchange = "PINK" }, found)
				?.ConId
		);
		// Two different US listings and nothing to choose by: ambiguous
		Assert.Null(ContractHelpers.PickMatch(new ContractDto { Symbol = "X" }, found));
	}

	[Fact]
	public void Of_one_contracts_venues_the_given_exchange_then_smart_is_picked()
	{
		var found = Found(Stock(7, "USD", "CBOE", ""), Stock(7, "USD", "SMART", ""));

		Assert.Equal(
			"SMART",
			ContractHelpers.PickMatch(new ContractDto { Symbol = "X" }, found)?.Exchange
		);
		Assert.Equal(
			"CBOE",
			ContractHelpers
				.PickMatch(new ContractDto { Symbol = "X", Exchange = "cboe" }, found)
				?.Exchange
		);
	}

	[Fact]
	public void With_no_security_type_the_first_type_found_wins()
	{
		var index = Stock(416904, "USD", "CBOE", "");
		index.SecurityType = "IND";

		var match = ContractHelpers.PickMatch(
			new ContractDto { Symbol = "SPX" },
			Found(Stock(1, "USD", "SMART", "NYSE"), index)
		);

		Assert.Equal(1, match?.ConId);
	}

	[Theory]
	[InlineData("20261218", 200, "C", "", 1001)]
	[InlineData("202612", 200, "call", "", 1001)]
	[InlineData("20261218", 200, "P", "", 1002)]
	[InlineData("20261218", 200, "", "", null)]
	[InlineData("20261218", 0, "C", "", null)]
	[InlineData("20261218", 200, "C", "10", null)]
	[InlineData("20270115", 200, "C", "100", 1004)]
	public void An_option_must_match_every_given_field(
		string expiry,
		double strike,
		string right,
		string multiplier,
		int? conId
	)
	{
		var found = Found(
			Option(1001, "20261218", 200, "C"),
			Option(1002, "20261218", 200, "P"),
			Option(1003, "20261218", 210, "C"),
			Option(1004, "20270115", 200, "C")
		);

		var match = ContractHelpers.PickMatch(
			new ContractDto
			{
				Symbol = "AAPL",
				SecurityType = "OPT",
				LastTradeDateOrContractMonth = expiry,
				Strike = strike,
				Right = right,
				Multiplier = multiplier,
			},
			found
		);

		Assert.Equal(conId, match?.ConId);
	}

	private static List<ContractDetailsDto> Found(params ContractDto[] contracts) =>
		[.. contracts.Select(x => new ContractDetailsDto { Contract = x })];

	private static ContractDto Stock(int conId, string currency, string exchange, string primary) =>
		new()
		{
			ConId = conId,
			Symbol = "X",
			SecurityType = "STK",
			Exchange = exchange,
			PrimaryExchange = primary,
			Currency = currency,
		};

	private static ContractDto Option(int conId, string expiry, double strike, string right) =>
		new()
		{
			ConId = conId,
			Symbol = "AAPL",
			SecurityType = "OPT",
			Exchange = "SMART",
			Currency = "USD",
			LastTradeDateOrContractMonth = expiry,
			Strike = strike,
			Right = right,
			Multiplier = "100",
		};
}
