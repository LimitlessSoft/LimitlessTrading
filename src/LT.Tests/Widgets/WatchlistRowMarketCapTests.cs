using LT.App.Helpers;
using LT.App.Widgets.Watchlist;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;

namespace LT.Tests.Widgets;

public class WatchlistRowMarketCapTests
{
	private readonly WatchlistRowViewModel _row = new(
		new ContractDto
		{
			Symbol = "AAPL",
			SecurityType = "STK",
			Currency = "USD",
		}
	);

	private void Tick(QuoteDto quote)
	{
		_row.Pending.Set(quote);
		_row.Flush();
	}

	[Fact]
	public void Market_cap_follows_the_price_once_the_share_count_is_known()
	{
		Tick(new QuoteDto { Close = 190 });
		Assert.Null(_row.MarketCapValue);
		Assert.Equal(FormatHelpers.Empty, _row.MarketCap);

		_row.SetSharesOutstanding(10_000_000_000);
		Assert.Equal(1_900_000_000_000, _row.MarketCapValue);

		Tick(new QuoteDto { Last = 250, Close = 190 });
		Assert.Equal(2_500_000_000_000, _row.MarketCapValue);
	}

	[Fact]
	public void No_share_count_leaves_the_column_empty()
	{
		_row.SetSharesOutstanding(null);
		Tick(new QuoteDto { Last = 250 });

		Assert.Null(_row.MarketCapValue);
		Assert.Equal(FormatHelpers.Empty, _row.MarketCap);
	}
}
