using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketDepth;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class OrderBookHelpersTests
{
	private readonly List<OrderBookLevelDto> _bids = [];
	private readonly List<OrderBookLevelDto> _asks = [];

	private static MarketDepthUpdateDto Bid(
		int position,
		MarketDepthOperation operation,
		double price = 0,
		decimal size = 0,
		string? exchange = null
	) =>
		new()
		{
			Position = position,
			Operation = operation,
			Side = MarketDepthSide.Bid,
			Price = price,
			Size = size,
			MarketMaker = exchange,
		};

	private bool Apply(MarketDepthUpdateDto update, int maxRows = 5) =>
		OrderBookHelpers.Apply(_bids, _asks, update, maxRows);

	private double[] BidPrices => [.. _bids.Select(x => x.Price)];

	[Fact]
	public void An_insert_pushes_the_rows_below_it_down()
	{
		Apply(Bid(0, MarketDepthOperation.Insert, 100));
		Apply(Bid(1, MarketDepthOperation.Insert, 98));
		Apply(Bid(1, MarketDepthOperation.Insert, 99));

		Assert.Equal([100, 99, 98], BidPrices);
		Assert.Empty(_asks);
	}

	[Fact]
	public void A_delete_pulls_the_rows_below_it_up()
	{
		Apply(Bid(0, MarketDepthOperation.Insert, 100));
		Apply(Bid(1, MarketDepthOperation.Insert, 99));
		Apply(Bid(2, MarketDepthOperation.Insert, 98));

		Assert.True(Apply(Bid(0, MarketDepthOperation.Delete)));

		Assert.Equal([99, 98], BidPrices);
	}

	[Fact]
	public void An_update_replaces_the_row_and_leaves_an_earlier_snapshot_alone()
	{
		Apply(Bid(0, MarketDepthOperation.Insert, 100, 40, "IEX"));
		var before = OrderBookHelpers.Snapshot(_bids, _asks, true);

		Apply(Bid(0, MarketDepthOperation.Update, 100, 80, "DRCTEDGE"));

		Assert.Equal((80m, "DRCTEDGE"), (_bids[0].Size, _bids[0].MarketMaker));
		Assert.Equal((40m, "IEX"), (before.Bids[0].Size, before.Bids[0].MarketMaker));
	}

	// Recorded on PAXOS: IB deletes rows past the end of the side as the client has built it
	[Fact]
	public void Deletes_and_updates_past_the_end_change_nothing()
	{
		Apply(Bid(0, MarketDepthOperation.Insert, 100));
		Apply(Bid(1, MarketDepthOperation.Insert, 99));

		Assert.False(Apply(Bid(2, MarketDepthOperation.Delete)));
		Assert.False(Apply(Bid(4, MarketDepthOperation.Update, 90, 1)));

		Assert.Equal([100, 99], BidPrices);
	}

	[Fact]
	public void A_side_never_grows_past_the_requested_rows()
	{
		for (var i = 0; i < 3; i++)
			Apply(Bid(i, MarketDepthOperation.Insert, 100 - i), maxRows: 3);

		Apply(Bid(0, MarketDepthOperation.Insert, 101), maxRows: 3);
		Assert.False(Apply(Bid(3, MarketDepthOperation.Insert, 50), maxRows: 3));

		Assert.Equal([101, 100, 99], BidPrices);
	}

	[Fact]
	public void An_insert_past_the_end_appends()
	{
		Apply(Bid(3, MarketDepthOperation.Insert, 100));

		Assert.Equal([100], BidPrices);
	}

	[Fact]
	public void Asks_and_bids_are_kept_apart()
	{
		Apply(Bid(0, MarketDepthOperation.Insert, 100));
		OrderBookHelpers.Apply(
			_bids,
			_asks,
			new MarketDepthUpdateDto
			{
				Position = 0,
				Operation = MarketDepthOperation.Insert,
				Side = MarketDepthSide.Ask,
				Price = 100.05,
				Size = 3,
			},
			5
		);

		Assert.Equal([100], BidPrices);
		Assert.Equal(100.05, Assert.Single(_asks).Price);
	}

	[Fact]
	public void Cumulative_sizes_run_from_the_best_row_out()
	{
		Apply(Bid(0, MarketDepthOperation.Insert, 100, 40));
		Apply(Bid(1, MarketDepthOperation.Insert, 99, 0.5m));
		Apply(Bid(2, MarketDepthOperation.Insert, 98, 100));

		Assert.Equal([40m, 40.5m, 140.5m], OrderBookHelpers.GetCumulativeSizes(_bids));
	}

	[Fact]
	public void A_smart_routed_stock_gets_smart_depth_or_its_listing_exchange()
	{
		var aapl = new ContractDto
		{
			ConId = 265598,
			Symbol = "AAPL",
			SecurityType = "STK",
			Exchange = "SMART",
			PrimaryExchange = "NASDAQ",
			Currency = "USD",
		};

		var (smart, isSmart) = OrderBookHelpers.GetDepthContract(aapl, true);
		var (listing, isListingSmart) = OrderBookHelpers.GetDepthContract(aapl, false);

		Assert.Equal(("SMART", true), (smart.Exchange, isSmart));
		Assert.Equal(("NASDAQ", false), (listing.Exchange, isListingSmart));
		Assert.Equal(265598, listing.ConId);
		Assert.Equal("SMART", aapl.Exchange);
	}

	[Fact]
	public void A_contract_on_one_exchange_keeps_it()
	{
		var btc = new ContractDto
		{
			ConId = 479624278,
			Symbol = "BTC",
			SecurityType = "CRYPTO",
			Exchange = "PAXOS",
			Currency = "USD",
		};

		var (contract, isSmart) = OrderBookHelpers.GetDepthContract(btc, true);

		Assert.Equal(("PAXOS", false), (contract.Exchange, isSmart));
	}

	[Theory]
	[InlineData(new[] { 343.84, 343.8 }, 2)]
	[InlineData(new[] { 1.11825, 1.1183 }, 5)]
	[InlineData(new[] { 80232.75, 80232.5 }, 2)]
	[InlineData(new[] { 0.1234567891 }, 6)]
	public void Price_decimals_show_every_price_exactly(double[] prices, int decimals)
	{
		var book = new OrderBookDto
		{
			Bids = [.. prices.Select(x => new OrderBookLevelDto { Price = x })],
		};

		Assert.Equal(decimals, OrderBookHelpers.GetPriceDecimals(book));
	}

	[Fact]
	public void Depth_refusals_get_a_readable_message()
	{
		var vow = new ContractDto
		{
			Symbol = "VOW",
			SecurityType = "STK",
			Exchange = "IBIS",
		};

		Assert.Contains("3 market depth", OrderBookHelpers.GetErrorMessage(309, vow));
		Assert.Contains("VOW on IBIS", OrderBookHelpers.GetErrorMessage(354, vow));
		Assert.Contains("10089", OrderBookHelpers.GetErrorMessage(10089, vow));
		Assert.Null(OrderBookHelpers.GetErrorMessage(200, vow));
	}
}
