using LT.Contracts.Constants;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

/// <summary>Trade sides, footprint rows (point of control, diagonal imbalances) and their numbers.</summary>
public class FootprintHelpersTests
{
	private static readonly DateTimeOffset T0 = new(2026, 10, 28, 13, 30, 0, TimeSpan.Zero);

	private static FootprintTradeDto Trade(double price, decimal size, FootprintSide side) =>
		new()
		{
			Time = T0,
			Price = price,
			Size = size,
			Side = side,
		};

	private static TickByTickDto Tick(double price, decimal size = 100, int second = 0) =>
		new()
		{
			Type = TickByTickType.AllLast,
			Time = T0.AddSeconds(second),
			Price = price,
			Size = size,
		};

	// A bar with (price, bid volume, ask volume) levels at a 0.01 tick
	private static FootprintBarDto Bar(params (double Price, decimal Bid, decimal Ask)[] levels)
	{
		var bar = new FootprintBarDto { Time = T0, TickSize = 0.01 };
		foreach (var (price, bid, ask) in levels)
		{
			if (bid > 0)
				FootprintHelpers.AddTrade(bar, Trade(price, bid, FootprintSide.Bid));
			if (ask > 0)
				FootprintHelpers.AddTrade(bar, Trade(price, ask, FootprintSide.Ask));
		}
		return bar;
	}

	[Theory]
	[InlineData(10.05, FootprintSide.Ask)] // at the ask
	[InlineData(10.07, FootprintSide.Ask)] // through the ask
	[InlineData(10.01, FootprintSide.Bid)] // at the bid
	[InlineData(9.99, FootprintSide.Bid)] // through the bid
	[InlineData(10.04, FootprintSide.Ask)] // inside, nearer the ask
	[InlineData(10.02, FootprintSide.Bid)] // inside, nearer the bid
	[InlineData(10.03, FootprintSide.Unknown)] // the midpoint
	public void The_quote_rule_compares_the_trade_with_the_bid_and_ask(
		double price,
		FootprintSide side
	) => Assert.Equal(side, FootprintHelpers.ClassifyByQuote(price, 10.01, 10.05));

	[Fact]
	public void No_usable_quote_tells_nothing() =>
		Assert.Equal(FootprintSide.Unknown, FootprintHelpers.ClassifyByQuote(10, 0, 10.05));

	[Fact]
	public void The_tick_rule_follows_the_price_and_keeps_the_side_on_a_zero_tick()
	{
		var trades = FootprintHelpers.ClassifyByTickRule(
			[Tick(10.02), Tick(10.02), Tick(10.01), Tick(10.01), Tick(10.03)],
			Tick(10.01)
		);

		Assert.Equal(
			[
				FootprintSide.Ask,
				FootprintSide.Ask,
				FootprintSide.Bid,
				FootprintSide.Bid,
				FootprintSide.Ask,
			],
			trades.Select(x => x.Side)
		);
		Assert.All(trades, x => Assert.True(x.ByTickRule));
	}

	[Fact]
	public void The_first_trade_with_nothing_before_it_is_unknown_until_the_price_moves()
	{
		var trades = FootprintHelpers.ClassifyByTickRule([Tick(10), Tick(10), Tick(9.99)], null);

		Assert.Equal(
			[FootprintSide.Unknown, FootprintSide.Unknown, FootprintSide.Bid],
			trades.Select(x => x.Side)
		);
	}

	[Fact]
	public void A_live_trade_goes_by_the_quote_and_falls_back_to_the_tick_rule()
	{
		var quote = new TickByTickDto
		{
			Type = TickByTickType.BidAsk,
			BidPrice = 10.01,
			AskPrice = 10.05,
		};

		var byQuote = FootprintHelpers.Classify(Tick(10.05), quote, 10.10, FootprintSide.Bid);
		var atMidpoint = FootprintHelpers.Classify(Tick(10.03), quote, 10.01, FootprintSide.Bid);
		var noQuote = FootprintHelpers.Classify(Tick(10.00), null, 10.01, FootprintSide.Ask);

		Assert.Equal((FootprintSide.Ask, false), (byQuote.Side, byQuote.ByTickRule));
		Assert.Equal((FootprintSide.Ask, true), (atMidpoint.Side, atMidpoint.ByTickRule));
		Assert.Equal((FootprintSide.Bid, true), (noQuote.Side, noQuote.ByTickRule));
	}

	[Fact]
	public void Trades_add_up_by_price_and_side_and_unknown_counts_half_on_each_side()
	{
		var bar = new FootprintBarDto { Time = T0, TickSize = 0.01 };

		FootprintHelpers.AddTrade(bar, Trade(10.01, 100, FootprintSide.Bid));
		FootprintHelpers.AddTrade(bar, Trade(10.01, 40, FootprintSide.Ask));
		// A sub-penny print lands on the nearest tick
		FootprintHelpers.AddTrade(bar, Trade(10.0099, 60, FootprintSide.Ask));
		FootprintHelpers.AddTrade(bar, Trade(10.02, 30, FootprintSide.Unknown));

		var level = bar.Levels[1001];
		Assert.Equal((100m, 100m), (level.BidVolume, level.AskVolume));
		Assert.Equal((15m, 15m), (bar.Levels[1002].BidVolume, bar.Levels[1002].AskVolume));
		Assert.Equal((115m, 115m, 0m), (bar.BidVolume, bar.AskVolume, bar.Delta));
	}

	[Fact]
	public void Rows_run_from_the_lowest_to_the_highest_price_with_empty_rows_between()
	{
		var rows = FootprintHelpers.GetRows(Bar((10.00, 5, 5), (10.03, 1, 1)), 1, 3);

		Assert.Equal([10.00, 10.01, 10.02, 10.03], rows.Select(x => Math.Round(x.Price, 2)));
		Assert.Equal([10m, 0m, 0m, 2m], rows.Select(x => x.Volume));
	}

	// SPY 14:50 on 2026-10-29: a 40-share print at 769.65, two dollars above the bar's high,
	// stretched the bar into 200 rows of 0 × 0
	[Fact]
	public void Prints_outside_the_bars_range_are_left_out_of_its_rows()
	{
		var rows = FootprintHelpers.GetRows(
			Bar((10.00, 5, 5), (10.01, 1, 1), (12.00, 0, 40)),
			1,
			3,
			10.00,
			10.01
		);

		Assert.Equal([10.00, 10.01], rows.Select(x => Math.Round(x.Price, 2)));
	}

	[Fact]
	public void A_side_counted_as_unknown_moves_to_the_side_found_later()
	{
		var bar = Bar();
		FootprintHelpers.AddTrade(bar, Trade(10.00, 30, FootprintSide.Unknown));

		FootprintHelpers.SetSide(bar, 1000, 30, FootprintSide.Bid);

		Assert.Equal((30m, 0m), (bar.Levels[1000].BidVolume, bar.Levels[1000].AskVolume));
		Assert.Equal((30m, 0m), (bar.BidVolume, bar.AskVolume));
	}

	[Fact]
	public void The_point_of_control_is_the_row_with_the_most_volume()
	{
		var rows = FootprintHelpers.GetRows(
			Bar((10.00, 5, 5), (10.01, 30, 20), (10.02, 10, 2)),
			1,
			3
		);

		Assert.Equal([false, true, false], rows.Select(x => x.IsPoc));
	}

	[Fact]
	public void Equal_volumes_put_the_point_of_control_on_the_lowest()
	{
		var rows = FootprintHelpers.GetRows(Bar((10.00, 5, 5), (10.01, 5, 5)), 1, 3);

		Assert.True(rows[0].IsPoc);
		Assert.False(rows[1].IsPoc);
	}

	[Fact]
	public void Imbalances_compare_diagonally_at_the_ratio()
	{
		// ask 10.02 (300) vs bid 10.01 (100): 300 % — buyers; ask 10.01 (50) vs bid 10.00 (20):
		// 250 % — none. bid 10.00 (20) vs ask 10.01 (50): none; bid 10.01 (100) vs ask 10.02
		// (300): none; bid 10.02 (90) vs ask above (none: the bar's top)
		var rows = FootprintHelpers.GetRows(
			Bar((10.00, 20, 10), (10.01, 100, 50), (10.02, 90, 300)),
			1,
			3
		);

		Assert.Equal([false, false, true], rows.Select(x => x.IsAskImbalance));
		Assert.Equal([false, false, false], rows.Select(x => x.IsBidImbalance));
	}

	[Fact]
	public void Sellers_imbalance_is_the_bid_against_the_ask_one_row_up()
	{
		// bid 10.00 (90) vs ask 10.01 (30): 300 % — sellers
		var rows = FootprintHelpers.GetRows(Bar((10.00, 90, 5), (10.01, 10, 30)), 1, 3);

		Assert.True(rows[0].IsBidImbalance);
		Assert.False(rows[1].IsBidImbalance);
	}

	[Fact]
	public void An_empty_opposite_row_inside_the_bar_is_an_imbalance_but_none_past_its_ends()
	{
		// 10.01 is empty: ask 10.02 against bid 10.01 (0) is one; the lowest row's ask has no row
		// below, the highest row's bid none above
		var rows = FootprintHelpers.GetRows(Bar((10.00, 0, 7), (10.02, 4, 2)), 1, 3);

		Assert.Equal([false, false, true], rows.Select(x => x.IsAskImbalance));
		Assert.Equal([false, false, false], rows.Select(x => x.IsBidImbalance));
	}

	[Fact]
	public void A_higher_ratio_finds_fewer_imbalances()
	{
		var bar = Bar((10.00, 20, 0), (10.01, 0, 50));

		Assert.True(FootprintHelpers.GetRows(bar, 1, 2)[1].IsAskImbalance);
		Assert.False(FootprintHelpers.GetRows(bar, 1, 3)[1].IsAskImbalance);
	}

	[Fact]
	public void Rows_of_several_ticks_sum_their_levels()
	{
		var rows = FootprintHelpers.GetRows(
			Bar((10.00, 1, 2), (10.01, 3, 4), (10.04, 5, 6), (10.05, 7, 8)),
			2,
			3
		);

		Assert.Equal([10.00, 10.02, 10.04], rows.Select(x => Math.Round(x.Price, 2)));
		Assert.Equal([10.01, 10.03, 10.05], rows.Select(x => Math.Round(x.HighPrice, 2)));
		Assert.Equal(
			[(4m, 6m), (0m, 0m), (12m, 14m)],
			rows.Select(x => (x.BidVolume, x.AskVolume))
		);
	}

	[Fact]
	public void A_bar_without_trades_has_no_rows() =>
		Assert.Empty(FootprintHelpers.GetRows(new FootprintBarDto { TickSize = 0.01 }, 1, 3));

	[Theory]
	[InlineData(20, 13, 1)]
	[InlineData(13, 13, 1)]
	[InlineData(10, 13, 2)]
	[InlineData(5, 13, 5)]
	[InlineData(1, 13, 20)]
	[InlineData(0.1, 13, 200)]
	[InlineData(0.5, 3, 10)]
	public void Rows_merge_ticks_in_1_2_5_steps_to_stay_tall_enough(
		double pixelsPerTick,
		double minRowHeight,
		int ticksPerRow
	) => Assert.Equal(ticksPerRow, FootprintHelpers.GetTicksPerRow(pixelsPerTick, minRowHeight));

	[Theory]
	[InlineData("0", "0")]
	[InlineData("0.25", "0.25")]
	[InlineData("12.5", "12.5")]
	[InlineData("845", "845")]
	[InlineData("9999", "9999")]
	[InlineData("12345", "12.3K")]
	[InlineData("123456", "123K")]
	[InlineData("1234567", "1.2M")]
	[InlineData("23456789", "23M")]
	public void Volumes_are_short(string volume, string text) =>
		Assert.Equal(text, FootprintHelpers.FormatVolume(decimal.Parse(volume)));

	[Fact]
	public void Combined_bars_sum_their_levels()
	{
		var profile = FootprintHelpers.Combine(
			[Bar((10.00, 1, 2)), Bar((10.00, 3, 4), (10.01, 5, 0))],
			0.01
		);

		Assert.Equal((4m, 6m), (profile.Levels[1000].BidVolume, profile.Levels[1000].AskVolume));
		Assert.Equal(5m, profile.Levels[1001].BidVolume);
		Assert.Equal((9m, 6m), (profile.BidVolume, profile.AskVolume));
	}

	[Fact]
	public void A_trade_belongs_to_the_bar_containing_it_or_the_one_to_come()
	{
		IReadOnlyList<BarDto> bars = [new() { Time = T0 }, new() { Time = T0.AddMinutes(1) }];

		Assert.Equal(
			T0,
			FootprintHelpers.GetBarTime(bars, T0.AddSeconds(59), ChartTimeframe.OneMinute)
		);
		Assert.Equal(
			T0.AddMinutes(3),
			FootprintHelpers.GetBarTime(
				bars,
				T0.AddMinutes(3).AddSeconds(5),
				ChartTimeframe.OneMinute
			)
		);
		Assert.Null(FootprintHelpers.GetBarTime(bars, T0.AddSeconds(-1), ChartTimeframe.OneMinute));
	}

	[Fact]
	public void The_backfill_stops_at_the_latest_day_first_bar()
	{
		var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
		IReadOnlyList<BarDto> bars =
		[
			new() { Time = new DateTimeOffset(2026, 10, 27, 19, 59, 0, TimeSpan.Zero) },
			new() { Time = new DateTimeOffset(2026, 10, 28, 8, 0, 0, TimeSpan.Zero) },
			new() { Time = new DateTimeOffset(2026, 10, 28, 13, 30, 0, TimeSpan.Zero) },
		];

		Assert.Equal(bars[1].Time, FootprintHelpers.GetSessionStart(bars, zone));
		Assert.Null(FootprintHelpers.GetSessionStart([], zone));
	}

	[Theory]
	[InlineData(0.01, 0.01)]
	[InlineData(0.25, 0.25)]
	[InlineData(0, FootprintConstants.FallbackTickSize)]
	[InlineData(double.MaxValue, FootprintConstants.FallbackTickSize)]
	public void The_tick_size_is_the_contract_minimum_tick(double minTick, double tickSize) =>
		Assert.Equal(tickSize, FootprintHelpers.GetTickSize(minTick));
}
