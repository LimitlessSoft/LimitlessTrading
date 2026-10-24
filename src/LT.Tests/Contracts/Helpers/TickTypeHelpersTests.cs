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
}
