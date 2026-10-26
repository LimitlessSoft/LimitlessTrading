using LT.Contracts.Constants;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class PriceAlertHelpersTests
{
	[Theory]
	[InlineData(190, 185, PriceAlertCondition.CrossingUp)]
	[InlineData(180, 185, PriceAlertCondition.CrossingDown)]
	[InlineData(185, 185, PriceAlertCondition.CrossingDown)]
	public void The_direction_is_told_from_the_current_price(
		double level,
		double current,
		PriceAlertCondition expected
	) => Assert.Equal(expected, PriceAlertHelpers.GetCondition(level, current));

	[Theory]
	[InlineData(PriceAlertCondition.CrossingUp, 189.99, false)]
	[InlineData(PriceAlertCondition.CrossingUp, 190, true)]
	[InlineData(PriceAlertCondition.CrossingUp, 195, true)]
	[InlineData(PriceAlertCondition.CrossingDown, 190.01, false)]
	[InlineData(PriceAlertCondition.CrossingDown, 190, true)]
	[InlineData(PriceAlertCondition.CrossingDown, 180, true)]
	public void A_price_at_or_past_the_level_crosses_it(
		PriceAlertCondition condition,
		double price,
		bool expected
	) => Assert.Equal(expected, PriceAlertHelpers.IsCrossed(condition, 190, price));

	[Fact]
	public void Stocks_compare_the_last_trade_delayed_or_not()
	{
		Assert.Equal(191.5, PriceAlertHelpers.GetPrice(new QuoteDto { Last = 191.5 }, "STK"));
		Assert.Equal(
			191.5,
			PriceAlertHelpers.GetPrice(new QuoteDto { Last = 191.5, IsDelayed = true }, "STK")
		);
		Assert.Null(PriceAlertHelpers.GetPrice(new QuoteDto { Bid = 191, Ask = 192 }, "STK"));
	}

	[Fact]
	public void Forex_compares_the_midpoint()
	{
		Assert.Equal(
			1.085,
			PriceAlertHelpers
				.GetPrice(new QuoteDto { Bid = 1.084, Ask = 1.086 }, MarketDataConstants.ForexType)!
				.Value,
			9
		);
		Assert.Null(
			PriceAlertHelpers.GetPrice(new QuoteDto { Bid = 1.084 }, MarketDataConstants.ForexType)
		);
	}

	[Fact]
	public void Only_contracts_with_an_active_alert_are_watched()
	{
		var watched = PriceAlertHelpers.GetWatchedConIds([
			Alert(1),
			Alert(1),
			Alert(2, triggered: true),
		]);

		Assert.Equal([1], watched);
	}

	[Fact]
	public void One_contract_too_many_is_refused_but_another_alert_on_a_watched_one_is_not()
	{
		var alerts = Enumerable
			.Range(1, PriceAlertConstants.MaxWatchedContracts)
			.Select(x => Alert(x))
			.Append(Alert(999, triggered: true))
			.ToList();

		Assert.Null(PriceAlertHelpers.GetLimitError(alerts, 1));
		Assert.NotNull(PriceAlertHelpers.GetLimitError(alerts, 999));
	}

	private static PriceAlertEntity Alert(int conId, bool triggered = false) =>
		new()
		{
			Id = Guid.NewGuid().ToString("N"),
			Contract = new ContractEntity { ConId = conId, Symbol = $"S{conId}" },
			Price = 100,
			TriggeredAt = triggered ? DateTimeOffset.UnixEpoch : null,
		};
}
