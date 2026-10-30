using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class ChartLiveUpdateHelpersTests
{
	private static readonly DateTimeOffset T0 = new(2026, 10, 23, 13, 30, 0, TimeSpan.Zero);

	private static readonly AppSettingsDto WithSpyOnTickByTick = new()
	{
		ChartLiveUpdateMode = ChartLiveUpdateMode.Quotes,
		TickerSettings =
		[
			new TickerSettingsDto
			{
				Contract = new ContractDto { ConId = 756733, Symbol = "SPY" },
				ChartLiveUpdateMode = ChartLiveUpdateMode.TickByTick,
			},
		],
	};

	[Fact]
	public void A_listed_ticker_uses_its_own_mode()
	{
		Assert.Equal(
			ChartLiveUpdateMode.TickByTick,
			ChartLiveUpdateHelpers.GetMode(WithSpyOnTickByTick, 756733)
		);
	}

	[Theory]
	[InlineData(265598)] // another ticker
	[InlineData(812345678)] // an option on SPY: its own contract, not listed
	[InlineData(0)] // no conId
	[InlineData(null)] // no contract
	public void Any_other_contract_uses_the_app_wide_mode(int? conId) =>
		Assert.Equal(
			ChartLiveUpdateMode.Quotes,
			ChartLiveUpdateHelpers.GetMode(WithSpyOnTickByTick, conId)
		);

	[Fact]
	public void Without_ticker_settings_every_chart_uses_the_app_wide_mode() =>
		Assert.Equal(
			ChartLiveUpdateMode.RealtimeBars,
			ChartLiveUpdateHelpers.GetMode(
				new AppSettingsDto { ChartLiveUpdateMode = ChartLiveUpdateMode.RealtimeBars },
				756733
			)
		);

	[Fact]
	public void A_trade_gives_its_price_and_a_quote_without_a_new_trade_gives_none()
	{
		var first = new QuoteDto
		{
			Last = 100,
			LastSize = 10,
			Volume = 1000,
		};
		var bidOnly = new QuoteDto
		{
			Bid = 99.9,
			Last = 100,
			LastSize = 10,
			Volume = 1000,
		};
		var samePrice = new QuoteDto
		{
			Last = 100,
			LastSize = 5,
			Volume = 1005,
		};

		Assert.Equal(100, ChartLiveUpdateHelpers.GetLivePrice(first, null, "STK"));
		Assert.Null(ChartLiveUpdateHelpers.GetLivePrice(bidOnly, first, "STK"));
		Assert.Equal(100, ChartLiveUpdateHelpers.GetLivePrice(samePrice, bidOnly, "STK"));
		Assert.Null(ChartLiveUpdateHelpers.GetLivePrice(new QuoteDto { Bid = 1 }, null, "STK"));
	}

	[Fact]
	public void Delayed_quotes_never_move_a_chart() =>
		Assert.Null(
			ChartLiveUpdateHelpers.GetLivePrice(
				new QuoteDto { Last = 100, IsDelayed = true },
				null,
				"STK"
			)
		);

	[Fact]
	public void Forex_uses_the_midpoint_when_it_changes()
	{
		var first = new QuoteDto { Bid = 1.1, Ask = 1.2 };
		var sameMid = new QuoteDto
		{
			Bid = 1.1,
			Ask = 1.2,
			BidSize = 5,
		};

		Assert.Equal(
			1.15,
			ChartLiveUpdateHelpers.GetLivePrice(first, null, MarketDataConstants.ForexType)!.Value,
			10
		);
		Assert.Null(
			ChartLiveUpdateHelpers.GetLivePrice(sameMid, first, MarketDataConstants.ForexType)
		);
	}

	[Fact]
	public void Prices_apply_only_while_5_second_bars_are_flowing()
	{
		Assert.False(ChartLiveUpdateHelpers.ShouldApplyPrice(T0, null));
		Assert.True(ChartLiveUpdateHelpers.ShouldApplyPrice(T0.AddSeconds(5), T0));
		Assert.False(
			ChartLiveUpdateHelpers.ShouldApplyPrice(
				T0 + ChartConstants.LivePriceMaxBarAge + TimeSpan.FromSeconds(1),
				T0
			)
		);
	}

	[Fact]
	public void A_5_second_bar_keeps_the_close_only_of_a_price_after_it_ended()
	{
		Assert.False(ChartLiveUpdateHelpers.KeepsClose(null, T0));
		Assert.False(ChartLiveUpdateHelpers.KeepsClose(T0.AddSeconds(3), T0));
		Assert.True(ChartLiveUpdateHelpers.KeepsClose(T0.AddSeconds(6), T0));
	}

	[Theory]
	[InlineData(ChartLiveUpdateMode.RealtimeBars, 1000, 0)]
	[InlineData(ChartLiveUpdateMode.Quotes, 1000, 0)]
	[InlineData(ChartLiveUpdateMode.QuotesThrottled, 1000, 1000)]
	[InlineData(ChartLiveUpdateMode.QuotesThrottled, 10, ChartConstants.RedrawIntervalMsMin)]
	[InlineData(ChartLiveUpdateMode.TickByTick, 1000, 0)]
	[InlineData(ChartLiveUpdateMode.TickByTickThrottled, 500, 500)]
	public void Only_the_throttled_mode_has_a_redraw_interval(
		ChartLiveUpdateMode mode,
		int intervalMs,
		int expectedMs
	) =>
		Assert.Equal(
			TimeSpan.FromMilliseconds(expectedMs),
			ChartLiveUpdateHelpers.GetRedrawInterval(mode, intervalMs)
		);

	[Fact]
	public void Every_mode_but_5_second_bars_uses_live_prices() =>
		Assert.Equal(
			[
				ChartLiveUpdateMode.Quotes,
				ChartLiveUpdateMode.QuotesThrottled,
				ChartLiveUpdateMode.TickByTick,
				ChartLiveUpdateMode.TickByTickThrottled,
			],
			Enum.GetValues<ChartLiveUpdateMode>().Where(ChartLiveUpdateHelpers.UsesLivePrices)
		);

	[Theory]
	[InlineData(ChartLiveUpdateMode.TickByTick, ChartLiveUpdateMode.Quotes)]
	[InlineData(ChartLiveUpdateMode.TickByTickThrottled, ChartLiveUpdateMode.QuotesThrottled)]
	[InlineData(ChartLiveUpdateMode.Quotes, null)]
	[InlineData(ChartLiveUpdateMode.QuotesThrottled, null)]
	[InlineData(ChartLiveUpdateMode.RealtimeBars, null)]
	public void Tick_by_tick_falls_back_to_quotes_and_quotes_to_5_second_bars(
		ChartLiveUpdateMode mode,
		ChartLiveUpdateMode? fallback
	) => Assert.Equal(fallback, ChartLiveUpdateHelpers.GetFallback(mode));

	[Fact]
	public void The_status_line_names_the_price_source()
	{
		Assert.Equal(
			"tick-by-tick",
			ChartLiveUpdateHelpers.GetSourceName(ChartLiveUpdateMode.TickByTickThrottled)
		);
		Assert.Equal("quotes", ChartLiveUpdateHelpers.GetSourceName(ChartLiveUpdateMode.Quotes));
		Assert.Null(ChartLiveUpdateHelpers.GetSourceName(ChartLiveUpdateMode.RealtimeBars));
	}

	[Fact]
	public void Forex_tick_by_tick_is_the_midpoint_everything_else_trades()
	{
		Assert.Equal(
			TickByTickType.MidPoint,
			ChartLiveUpdateHelpers.GetTickByTickType(MarketDataConstants.ForexType)
		);
		Assert.Equal(TickByTickType.Last, ChartLiveUpdateHelpers.GetTickByTickType("STK"));
	}
}
