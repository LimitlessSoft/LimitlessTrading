using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class WatchlistStreamingHelpersTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 28, 15, 0, 0, TimeSpan.Zero);

	[Fact]
	public void The_live_range_spans_the_shown_rows_and_a_buffer_within_the_list()
	{
		Assert.Equal((5, 25), WatchlistStreamingHelpers.GetLiveRange(60, [10, 12, 19], 5));
		Assert.Equal((0, 8), WatchlistStreamingHelpers.GetLiveRange(60, [2, 0, 1], 5));
		Assert.Equal((52, 60), WatchlistStreamingHelpers.GetLiveRange(60, [57, 59], 5));
		// Rows no longer in the list (-1) are ignored
		Assert.Equal((0, 3), WatchlistStreamingHelpers.GetLiveRange(3, [-1, 1], 5));
	}

	[Fact]
	public void No_live_range_when_nothing_is_shown()
	{
		Assert.Null(WatchlistStreamingHelpers.GetLiveRange(60, [], 5));
		Assert.Null(WatchlistStreamingHelpers.GetLiveRange(0, [0], 5));
	}

	[Fact]
	public void A_snapshot_is_due_without_a_quote_or_once_the_last_is_old_but_not_before_a_retry()
	{
		var interval = TimeSpan.FromSeconds(30);

		Assert.True(WatchlistStreamingHelpers.IsSnapshotDue(null, null, Now, interval));
		Assert.False(
			WatchlistStreamingHelpers.IsSnapshotDue(Now.AddSeconds(-29), null, Now, interval)
		);
		Assert.True(
			WatchlistStreamingHelpers.IsSnapshotDue(Now.AddSeconds(-30), null, Now, interval)
		);
		Assert.False(
			WatchlistStreamingHelpers.IsSnapshotDue(null, Now.AddSeconds(1), Now, interval)
		);
		Assert.True(WatchlistStreamingHelpers.IsSnapshotDue(null, Now, Now, interval));
	}

	[Fact]
	public void Quote_lines_are_shared_by_listing_generic_ticks_and_market_data_type()
	{
		var smart = new ContractDto
		{
			ConId = 1,
			Exchange = "SMART",
			Symbol = "AAPL",
		};
		var sameListing = new ContractDto
		{
			ConId = 1,
			Exchange = "smart",
			Symbol = "other",
		};
		var key = MarketDataShareHelpers.GetShareKey(smart, "", MarketDataType.Live);

		Assert.Equal(
			key,
			MarketDataShareHelpers.GetShareKey(sameListing, " ", MarketDataType.Live)
		);
		Assert.NotEqual(key, MarketDataShareHelpers.GetShareKey(smart, "106", MarketDataType.Live));
		Assert.NotEqual(key, MarketDataShareHelpers.GetShareKey(smart, "", MarketDataType.Delayed));
		Assert.NotEqual(
			key,
			MarketDataShareHelpers.GetShareKey(
				new ContractDto { ConId = 1, Exchange = "NASDAQ" },
				"",
				MarketDataType.Live
			)
		);
		// Without a conId the fields decide
		var bySymbol = new ContractDto
		{
			Symbol = "EUR",
			SecurityType = "CASH",
			Currency = "USD",
		};
		Assert.Equal(
			MarketDataShareHelpers.GetShareKey(bySymbol, "", null),
			MarketDataShareHelpers.GetShareKey(
				new ContractDto
				{
					Symbol = "eur",
					SecurityType = "CASH",
					Currency = "USD",
				},
				"",
				null
			)
		);
		Assert.NotEqual(
			MarketDataShareHelpers.GetShareKey(bySymbol, "", null),
			MarketDataShareHelpers.GetShareKey(
				new ContractDto
				{
					Symbol = "EUR",
					SecurityType = "CASH",
					Currency = "GBP",
				},
				"",
				null
			)
		);
	}
}
