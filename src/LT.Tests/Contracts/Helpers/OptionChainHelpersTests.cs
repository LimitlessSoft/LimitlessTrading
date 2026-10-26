using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.Options;
using LT.Contracts.Enums;
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
	[InlineData(145, "C", true)]
	[InlineData(155, "C", false)]
	[InlineData(150, "C", false)]
	[InlineData(145, "P", false)]
	[InlineData(155, "P", true)]
	[InlineData(150, "P", false)]
	public void Calls_are_in_the_money_below_the_price_and_puts_above(
		double strike,
		string right,
		bool inTheMoney
	) => Assert.Equal(inTheMoney, OptionChainHelpers.IsInTheMoney(strike, right, 150));

	[Theory]
	[InlineData(null)]
	[InlineData(0d)]
	public void Nothing_is_in_the_money_when_the_price_is_unknown(double? price)
	{
		Assert.False(OptionChainHelpers.IsInTheMoney(100, "C", price));
		Assert.False(OptionChainHelpers.IsInTheMoney(200, "P", price));
		Assert.Null(
			OptionChainHelpers.GetAtmStrike(
				OptionChainHelpers.BuildStrikes([Option(100, "C"), Option(200, "C")]),
				price
			)
		);
	}

	[Theory]
	[InlineData(151, 150d)]
	[InlineData(153, 155d)]
	[InlineData(152.5, 150d)]
	[InlineData(500, 195d)]
	public void Atm_strike_is_the_nearest_one(double price, double expected)
	{
		var strikes = OptionChainHelpers.BuildStrikes(
			Enumerable.Range(0, 20).Select(i => Option(100 + i * 5, "C"))
		);

		Assert.Equal(expected, OptionChainHelpers.GetAtmStrike(strikes, price));
	}

	[Fact]
	public void Moneyness_follows_the_price()
	{
		// a strike that was an in-the-money call turns into an in-the-money put as the price drops
		Assert.True(OptionChainHelpers.IsInTheMoney(150, "C", 152));
		Assert.False(OptionChainHelpers.IsInTheMoney(150, "P", 152));
		Assert.False(OptionChainHelpers.IsInTheMoney(150, "C", 148));
		Assert.True(OptionChainHelpers.IsInTheMoney(150, "P", 148));
	}

	// 100, 105 … 195: index i is strike 100 + 5i
	private static IReadOnlyList<OptionStrikeDto> Ladder(int count = 20) =>
		OptionChainHelpers.BuildStrikes(
			Enumerable.Range(0, count).Select(i => Option(100 + i * 5, "C"))
		);

	[Theory]
	[InlineData(10, 8, 0, 9)]
	[InlineData(20, 1, 0, 9)]
	[InlineData(20, 18, 10, 19)]
	[InlineData(5, 2, 0, 4)]
	public void Window_is_clamped_to_the_list(int strikeCount, int center, int from, int to) =>
		Assert.Equal((from, to), OptionChainHelpers.GetWindow(strikeCount, center, 8));

	[Fact]
	public void Window_stays_while_the_atm_strike_is_within_the_threshold()
	{
		// centred on 150 (index 10); 165 is three strikes off
		Assert.Null(OptionChainHelpers.GetRecenterTarget(Ladder(40), 10, 164, 8, 3));
		Assert.Null(OptionChainHelpers.GetRecenterTarget(Ladder(40), 10, 136, 8, 3));
	}

	[Fact]
	public void Window_recentres_on_the_atm_strike_past_the_threshold()
	{
		Assert.Equal(14, OptionChainHelpers.GetRecenterTarget(Ladder(40), 10, 169, 8, 3));
		Assert.Equal(6, OptionChainHelpers.GetRecenterTarget(Ladder(40), 10, 131, 8, 3));
	}

	[Fact]
	public void Price_wobbling_around_a_strike_never_recentres()
	{
		var strikes = Ladder(40);
		var center = 10;
		foreach (var price in new[] { 149.0, 153, 147, 156, 144, 151, 158, 142 })
			Assert.Null(OptionChainHelpers.GetRecenterTarget(strikes, center, price, 8, 3));
	}

	[Fact]
	public void Recentring_onto_an_atm_that_is_the_new_centre_settles()
	{
		var strikes = Ladder(40);
		var target = OptionChainHelpers.GetRecenterTarget(strikes, 10, 171, 8, 3);

		Assert.Equal(14, target);
		Assert.Null(OptionChainHelpers.GetRecenterTarget(strikes, target!.Value, 171, 8, 3));
	}

	[Fact]
	public void Window_at_the_edge_of_the_strike_list_does_not_move_when_it_would_not_change()
	{
		// 5 strikes: every window already holds them all, however far the ATM is off centre
		Assert.Null(OptionChainHelpers.GetRecenterTarget(Ladder(5), 0, 1000, 8, 3));
		// price far past the top: ATM is the last strike
		Assert.Equal(39, OptionChainHelpers.GetRecenterTarget(Ladder(40), 30, 1000, 8, 3));
		Assert.Equal(0, OptionChainHelpers.GetRecenterTarget(Ladder(40), 9, 1, 8, 3));
	}

	[Theory]
	[InlineData(null)]
	[InlineData(0d)]
	public void Window_stays_when_the_price_is_unknown(double? price) =>
		Assert.Null(OptionChainHelpers.GetRecenterTarget(Ladder(40), 10, price, 8, 3));

	private static OptionStrikeDto TradableStrike() =>
		new()
		{
			Strike = 150,
			Call = WithConId(Option(150, "C"), 11),
			Put = WithConId(Option(150, "P"), 12),
		};

	private static ContractDto WithConId(ContractDto contract, int conId)
	{
		contract.ConId = conId;
		return contract;
	}

	[Theory]
	[InlineData(OptionQuoteColumn.Ask, OrderAction.Buy)]
	[InlineData(OptionQuoteColumn.Bid, OrderAction.Sell)]
	[InlineData(OptionQuoteColumn.Last, null)]
	[InlineData(OptionQuoteColumn.Volume, null)]
	public void Ask_buys_bid_sells_and_other_columns_do_not_trade(
		OptionQuoteColumn column,
		OrderAction? action
	) => Assert.Equal(action, OptionChainHelpers.GetQuickOrderAction(column));

	[Theory]
	[InlineData("C", OptionQuoteColumn.Ask, 11, OrderAction.Buy)]
	[InlineData("C", OptionQuoteColumn.Bid, 11, OrderAction.Sell)]
	[InlineData("P", OptionQuoteColumn.Ask, 12, OrderAction.Buy)]
	[InlineData("P", OptionQuoteColumn.Bid, 12, OrderAction.Sell)]
	public void Quick_order_is_at_market_for_the_day(
		string right,
		OptionQuoteColumn column,
		int conId,
		OrderAction action
	)
	{
		var request = OptionChainHelpers.BuildQuickOrder(TradableStrike(), right, column, "U1", 1);

		Assert.NotNull(request);
		Assert.Equal(conId, request.Contract!.ConId);
		Assert.Equal(right, request.Contract.Right);
		Assert.Equal(action, request.Action);
		Assert.Equal(OrderType.Market, request.OrderType);
		Assert.Equal(1m, request.Quantity);
		Assert.Null(request.CashAmount);
		Assert.Null(request.LimitPrice);
		Assert.Equal(TimeInForce.Day, request.TimeInForce);
		Assert.Equal("U1", request.Account);
	}

	[Fact]
	public void No_quick_order_for_other_columns_or_a_missing_side()
	{
		var strike = TradableStrike();
		strike.Put = null;

		Assert.Null(
			OptionChainHelpers.BuildQuickOrder(strike, "C", OptionQuoteColumn.Last, "U1", 1)
		);
		Assert.Null(
			OptionChainHelpers.BuildQuickOrder(strike, "C", OptionQuoteColumn.Volume, "U1", 1)
		);
		Assert.Null(
			OptionChainHelpers.BuildQuickOrder(strike, "P", OptionQuoteColumn.Ask, "U1", 1)
		);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(100)]
	public void Quick_order_sends_the_chosen_quantity(int quantity)
	{
		var request = OptionChainHelpers.BuildQuickOrder(
			TradableStrike(),
			"C",
			OptionQuoteColumn.Ask,
			"U1",
			quantity
		);

		Assert.Equal(quantity, request!.Quantity);
		Assert.Null(OptionChainHelpers.GetQuickOrderQuantityError(quantity));
	}

	[Theory]
	[InlineData(null)]
	[InlineData(0d)]
	[InlineData(-1d)]
	[InlineData(101d)]
	[InlineData(2.5)]
	public void No_quick_order_for_an_empty_or_out_of_range_quantity(double? quantity)
	{
		var q = (decimal?)quantity;

		Assert.False(OptionChainHelpers.IsValidQuickOrderQuantity(q));
		Assert.Null(
			OptionChainHelpers.BuildQuickOrder(
				TradableStrike(),
				"C",
				OptionQuoteColumn.Ask,
				"U1",
				q
			)
		);
		Assert.Equal(
			"Set a one-click quantity of 1 to 100 whole contracts.",
			OptionChainHelpers.GetQuickOrderQuantityError(q)
		);
	}

	[Theory]
	[InlineData(null, 1)]
	[InlineData(7, 7)]
	[InlineData(0, 1)]
	[InlineData(1000, 1)]
	public void Saved_quick_order_quantity_falls_back_to_the_default(int? saved, int expected) =>
		Assert.Equal(expected, OptionChainHelpers.GetQuickOrderQuantityOrDefault(saved));

	[Fact]
	public void Quick_trade_needs_a_price_in_the_clicked_column()
	{
		var strike = TradableStrike();
		var bidOnly = new QuoteDto { Bid = 1.2 };

		Assert.True(OptionChainHelpers.CanQuickTrade(strike, "C", OptionQuoteColumn.Bid, bidOnly));
		Assert.False(OptionChainHelpers.CanQuickTrade(strike, "C", OptionQuoteColumn.Ask, bidOnly));
		Assert.False(OptionChainHelpers.CanQuickTrade(strike, "C", OptionQuoteColumn.Bid, null));
		Assert.False(
			OptionChainHelpers.CanQuickTrade(
				strike,
				"C",
				OptionQuoteColumn.Last,
				new QuoteDto
				{
					Bid = 1,
					Ask = 1.1,
					Last = 1.05,
				}
			)
		);
		strike.Call = null;
		Assert.False(OptionChainHelpers.CanQuickTrade(strike, "C", OptionQuoteColumn.Bid, bidOnly));
	}

	[Fact]
	public void Repeat_clicks_within_the_window_are_ignored()
	{
		var first = new DateTimeOffset(2026, 10, 26, 15, 0, 0, TimeSpan.Zero);

		Assert.False(OptionChainHelpers.IsRepeatClick(null, first));
		Assert.True(OptionChainHelpers.IsRepeatClick(first, first.AddMilliseconds(120)));
		Assert.True(OptionChainHelpers.IsRepeatClick(first, first.AddMilliseconds(499)));
		Assert.False(OptionChainHelpers.IsRepeatClick(first, first.AddMilliseconds(500)));
	}

	[Fact]
	public void Repeat_guard_keys_a_cell_by_contract_and_side()
	{
		var strike = TradableStrike();
		string Key(string right, OptionQuoteColumn column) =>
			OptionChainHelpers.GetQuickOrderKey(
				OptionChainHelpers.BuildQuickOrder(strike, right, column, "U1", 1)!
			);

		Assert.Equal(Key("C", OptionQuoteColumn.Ask), Key("C", OptionQuoteColumn.Ask));
		Assert.NotEqual(Key("C", OptionQuoteColumn.Ask), Key("C", OptionQuoteColumn.Bid));
		Assert.NotEqual(Key("C", OptionQuoteColumn.Ask), Key("P", OptionQuoteColumn.Ask));
	}

	[Fact]
	public void Expected_move_is_price_times_iv_times_root_time()
	{
		// 200 × 25% × √0.25 = 25
		Assert.Equal(25, OptionChainHelpers.GetExpectedMove(200, 0.25, 0.25)!.Value, 9);
	}

	[Theory]
	[InlineData(null, 0.25, 0.1)]
	[InlineData(200d, null, 0.1)]
	[InlineData(0d, 0.25, 0.1)]
	[InlineData(200d, 0d, 0.1)]
	[InlineData(200d, 0.25, 0d)]
	public void Expected_move_is_unknown_without_price_iv_or_time(
		double? price,
		double? iv,
		double years
	) => Assert.Null(OptionChainHelpers.GetExpectedMove(price, iv, years));

	[Fact]
	public void Years_to_expiration_count_calendar_days_and_stop_at_zero()
	{
		var expiration = new DateTimeOffset(2026, 11, 20, 21, 0, 0, TimeSpan.Zero);

		Assert.Equal(
			73 / 365.0,
			OptionChainHelpers.GetYearsToExpiration(expiration, expiration.AddDays(-73)),
			9
		);
		Assert.Equal(
			0,
			OptionChainHelpers.GetYearsToExpiration(expiration, expiration.AddHours(1))
		);
	}

	[Fact]
	public void Time_to_expiration_shrinking_to_zero_drops_the_bands()
	{
		var expiration = new DateTimeOffset(2026, 11, 20, 21, 0, 0, TimeSpan.Zero);
		var nearly = OptionChainHelpers.GetYearsToExpiration(expiration, expiration.AddMinutes(-1));
		var sigma = OptionChainHelpers.GetExpectedMove(200, 0.25, nearly);

		Assert.NotNull(sigma);
		Assert.True(sigma < 0.2);
		Assert.Null(
			OptionChainHelpers.GetExpectedMove(
				200,
				0.25,
				OptionChainHelpers.GetYearsToExpiration(expiration, expiration)
			)
		);
	}

	[Fact]
	public void Expiration_is_the_new_york_close_of_the_expiry_date()
	{
		// 16:00 EST (UTC-5) in November, 16:00 EDT (UTC-4) in July
		Assert.Equal(
			new DateTimeOffset(2026, 11, 20, 21, 0, 0, TimeSpan.Zero),
			OptionChainHelpers.GetExpirationTime("20261120")
		);
		Assert.Equal(
			new DateTimeOffset(2026, 7, 17, 20, 0, 0, TimeSpan.Zero),
			OptionChainHelpers.GetExpirationTime("20260717")
		);
		Assert.Null(OptionChainHelpers.GetExpirationTime("202611"));
	}

	[Theory]
	[InlineData(110, 10, 1.0, SigmaBand.WithinOne)]
	[InlineData(90, 10, -1.0, SigmaBand.WithinOne)]
	[InlineData(100, 10, 0.0, SigmaBand.WithinOne)]
	[InlineData(110.5, 10, 1.05, SigmaBand.WithinTwo)]
	[InlineData(120, 10, 2.0, SigmaBand.WithinTwo)]
	[InlineData(79, 10, -2.1, SigmaBand.WithinThree)]
	[InlineData(130, 10, 3.0, SigmaBand.WithinThree)]
	[InlineData(130.5, 10, 3.05, SigmaBand.BeyondThree)]
	[InlineData(50, 10, -5.0, SigmaBand.BeyondThree)]
	public void Sigma_bands_include_their_upper_bound(
		double strike,
		double sigma,
		double distance,
		SigmaBand band
	)
	{
		var d = OptionChainHelpers.GetSigmaDistance(strike, 100, sigma);

		Assert.Equal(distance, d!.Value, 9);
		Assert.Equal(band, OptionChainHelpers.GetSigmaBand(d));
	}

	[Theory]
	[InlineData(null, 10d)]
	[InlineData(0d, 10d)]
	[InlineData(100d, null)]
	[InlineData(100d, 0d)]
	public void No_band_when_price_or_sigma_is_unknown(double? price, double? sigma)
	{
		var d = OptionChainHelpers.GetSigmaDistance(110, price, sigma);

		Assert.Null(d);
		Assert.Equal(SigmaBand.None, OptionChainHelpers.GetSigmaBand(d));
		Assert.Null(OptionChainHelpers.FormatSigma(d));
	}

	[Theory]
	[InlineData(1.44, "+1.4σ")]
	[InlineData(-0.26, "-0.3σ")]
	[InlineData(0d, "0.0σ")]
	public void Sigma_distance_is_shown_with_one_decimal_and_a_sign(double d, string text) =>
		Assert.Equal(text, OptionChainHelpers.FormatSigma(d));

	[Theory]
	[InlineData("202611", true)]
	[InlineData("20261120", true)]
	[InlineData("2026-11", false)]
	public void Expiry_format(string expiry, bool valid) =>
		Assert.Equal(valid, OptionChainHelpers.ExpiryRegex().IsMatch(expiry));
}
