using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class TickTypeHelpersTests
{
	[Fact]
	public void Live_and_delayed_ticks_map_to_the_same_fields()
	{
		var quote = new QuoteDto();

		TickTypeHelpers.Apply(
			quote,
			new MarketDataTickDto { Field = TickTypeHelpers.Last, Price = 10 }
		);
		Assert.False(quote.IsDelayed);
		TickTypeHelpers.Apply(
			quote,
			new MarketDataTickDto { Field = TickTypeHelpers.DelayedBid, Price = 9.9 }
		);
		TickTypeHelpers.Apply(
			quote,
			new MarketDataTickDto { Field = TickTypeHelpers.DelayedVolume, Size = 1200 }
		);

		Assert.Equal((10d, 9.9d, 1200m), (quote.Last, quote.Bid, quote.Volume));
		Assert.True(quote.IsDelayed);
	}

	[Fact]
	public void The_52_week_low_and_high_of_generic_tick_165_are_kept()
	{
		var quote = new QuoteDto();

		Assert.True(
			TickTypeHelpers.Apply(
				quote,
				new MarketDataTickDto { Field = TickTypeHelpers.Week52Low, Price = 480.5 }
			)
		);
		Assert.True(
			TickTypeHelpers.Apply(
				quote,
				new MarketDataTickDto { Field = TickTypeHelpers.Week52High, Price = 690.25 }
			)
		);

		Assert.Equal((480.5d, 690.25d), (quote.Week52Low, quote.Week52High));
	}

	[Fact]
	public void Volume_rate_is_kept_in_shares_per_minute_and_zero_is_a_rate()
	{
		var quote = new QuoteDto();

		// As recorded from IB: a generic tick (45), its value in shares per minute
		Assert.True(
			TickTypeHelpers.Apply(
				quote,
				new MarketDataTickDto { Field = TickTypeHelpers.VolumeRate, Value = 39944 }
			)
		);
		Assert.Equal(39944, quote.VolumeRate);

		TickTypeHelpers.Apply(
			quote,
			new MarketDataTickDto { Field = TickTypeHelpers.VolumeRate, Value = 0 }
		);
		Assert.Equal(0, quote.VolumeRate);

		TickTypeHelpers.Apply(
			quote,
			new MarketDataTickDto { Field = TickTypeHelpers.VolumeRate, Value = -1 }
		);
		Assert.Null(quote.VolumeRate);
	}

	[Fact]
	public void Average_volume_per_minute_spreads_the_daily_average_over_a_regular_session()
	{
		Assert.Equal(
			111_545.73m,
			Math.Round(
				TickTypeHelpers
					.GetAverageVolumePerMinute(new QuoteDto { AverageVolume = 43_502_835 })!
					.Value,
				2
			)
		);
		Assert.Null(TickTypeHelpers.GetAverageVolumePerMinute(new QuoteDto()));
	}

	[Fact]
	public void Average_volume_is_kept_in_shares_and_non_positive_means_unknown()
	{
		var quote = new QuoteDto();

		Assert.True(
			TickTypeHelpers.Apply(
				quote,
				new MarketDataTickDto { Field = TickTypeHelpers.AverageVolume, Size = 43502835 }
			)
		);
		Assert.Equal(43502835m, quote.AverageVolume);
		Assert.False(quote.IsDelayed);

		TickTypeHelpers.Apply(
			quote,
			new MarketDataTickDto { Field = TickTypeHelpers.AverageVolume, Size = 0 }
		);
		Assert.Null(quote.AverageVolume);
	}

	[Fact]
	public void Minus_one_price_means_no_data()
	{
		var quote = new QuoteDto { Bid = 5 };

		TickTypeHelpers.Apply(
			quote,
			new MarketDataTickDto { Field = TickTypeHelpers.Bid, Price = -1 }
		);

		Assert.Null(quote.Bid);
	}

	[Fact]
	public void Option_implied_volatility_is_kept_and_non_positive_means_unknown()
	{
		var quote = new QuoteDto();

		TickTypeHelpers.Apply(
			quote,
			new MarketDataTickDto { Field = TickTypeHelpers.OptionImpliedVolatility, Value = 0.31 }
		);
		Assert.Equal(0.31, quote.ImpliedVolatility);

		TickTypeHelpers.Apply(
			quote,
			new MarketDataTickDto { Field = TickTypeHelpers.OptionImpliedVolatility, Value = -1 }
		);
		Assert.Null(quote.ImpliedVolatility);
	}

	[Fact]
	public void Last_timestamp_is_parsed()
	{
		var quote = new QuoteDto();

		TickTypeHelpers.Apply(
			quote,
			new MarketDataTickDto
			{
				Field = TickTypeHelpers.LastTimestamp,
				StringValue = "1761220800",
			}
		);

		Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1761220800), quote.LastTime);
	}

	[Fact]
	public void Change_is_relative_to_previous_close()
	{
		var quote = new QuoteDto { Last = 110, Close = 100 };

		Assert.Equal(10, TickTypeHelpers.GetChange(quote));
		Assert.Equal(10, TickTypeHelpers.GetChangePercent(quote));
		Assert.Equal(100, TickTypeHelpers.GetDisplayPrice(new QuoteDto { Close = 100 }));
	}

	[Fact]
	public void Model_greeks_and_open_interest_fold_into_the_quote()
	{
		var quote = new QuoteDto();
		var model = new OptionComputationDto { Gamma = 0.11, ImpliedVolatility = 0.25 };

		Assert.True(
			TickTypeHelpers.Apply(
				quote,
				new MarketDataTickDto
				{
					Field = TickTypeHelpers.ModelOptionComputation,
					Option = model,
				}
			)
		);
		TickTypeHelpers.Apply(
			quote,
			new MarketDataTickDto { Field = TickTypeHelpers.CallOpenInterest, Size = 0 }
		);
		TickTypeHelpers.Apply(
			quote,
			new MarketDataTickDto { Field = TickTypeHelpers.PutOpenInterest, Size = 41057 }
		);

		Assert.Same(model, quote.ModelGreeks);
		Assert.Equal((0m, 41057m), (quote.CallOpenInterest, quote.PutOpenInterest));
	}

	[Fact]
	public void A_bid_computation_is_not_the_model()
	{
		var quote = new QuoteDto();

		var applied = TickTypeHelpers.Apply(
			quote,
			new MarketDataTickDto
			{
				Field = 10,
				Option = new OptionComputationDto { Gamma = 0.1 },
			}
		);

		Assert.False(applied);
		Assert.Null(quote.ModelGreeks);
	}
}
