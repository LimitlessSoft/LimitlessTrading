using LT.App.Widgets.Chart;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Widgets;

/// <summary>
/// A chart's footprints: bars show their trades as the pages bring them (paged back from each
/// bar's end), the visible bars first, then the session; the tick rule across pages; live trades
/// from where the pages start; what bars without trades show.
/// </summary>
public class ChartFootprintTests
{
	// 1-minute bars 13:30, 13:31, 13:32; live from 13:32:30 (the 13:32 bar is forming)
	private static readonly DateTimeOffset T0 = new(2026, 10, 28, 13, 30, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset LiveFrom = T0.AddSeconds(150);

	private readonly FakeTimeProvider _time = new(LiveFrom);
	private readonly IReadOnlyList<BarDto> _bars =
	[
		new() { Time = T0 },
		new() { Time = T0.AddMinutes(1) },
		new() { Time = T0.AddMinutes(2) },
	];
	private int _changes;

	private ChartFootprint Create(DateTimeOffset? sessionStart = null, int maxRequests = 100) =>
		new(
			ChartTimeframe.OneMinute,
			0.01,
			LiveFrom,
			sessionStart ?? T0,
			() => _bars,
			_time,
			() => _changes++,
			x => x(),
			maxRequests
		);

	private static TickByTickDto Tick(int second, double price, decimal size = 100) =>
		new()
		{
			Type = TickByTickType.AllLast,
			Time = T0.AddSeconds(second),
			Price = price,
			Size = size,
		};

	private static FootprintTradeDto Live(int second, double price, FootprintSide side) =>
		new()
		{
			Time = T0.AddSeconds(second),
			Price = price,
			Size = 10,
			Side = side,
		};

	[Fact]
	public void The_first_page_ends_where_live_trades_start()
	{
		using var footprint = Create();

		Assert.Equal(LiveFrom, footprint.GetNextPageEnd());
		Assert.Equal(FootprintBarState.Loading, footprint.GetState(T0.AddMinutes(2)));
	}

	[Fact]
	public void A_bar_shows_the_trades_of_its_first_page_before_the_rest_are_in()
	{
		using var footprint = Create();

		// 13:32:10 … 13:32:29: the end of the 13:32 bar only
		footprint.AddPage([Tick(130, 10.00), Tick(140, 10.01), Tick(149, 10.00)], LiveFrom);

		Assert.Equal(FootprintBarState.Partial, footprint.GetState(T0.AddMinutes(2)));
		var bar = footprint.Get(T0.AddMinutes(2))!;
		Assert.Equal(300m, bar.BidVolume + bar.AskVolume);
		Assert.Equal(T0.AddSeconds(130), footprint.GetLoadedFrom(T0.AddMinutes(2)));
		Assert.True(_changes > 0);
	}

	[Fact]
	public void The_forming_bar_shows_live_trades_before_any_page()
	{
		using var footprint = Create();

		footprint.AddLiveTrade(Live(155, 10.02, FootprintSide.Ask));

		Assert.Equal(FootprintBarState.Partial, footprint.GetState(T0.AddMinutes(2)));
		Assert.Equal(10m, footprint.Get(T0.AddMinutes(2))!.AskVolume);
	}

	[Fact]
	public void Every_visible_bar_gets_a_page_before_any_gets_a_second()
	{
		using var footprint = Create();
		footprint.AddPage([Tick(140, 10), Tick(149, 10)], LiveFrom);

		// 13:31 and 13:30 have nothing yet: their ends come next, newest first
		Assert.Equal(T0.AddMinutes(2), footprint.GetNextPageEnd());
		footprint.AddPage([Tick(110, 10), Tick(119, 10)], T0.AddMinutes(2));
		Assert.Equal(T0.AddMinutes(1), footprint.GetNextPageEnd());
		footprint.AddPage([Tick(50, 10), Tick(59, 10)], T0.AddMinutes(1));

		// Then the rest of each, newest first, from where it got to
		Assert.Equal(T0.AddSeconds(140), footprint.GetNextPageEnd());
	}

	[Fact]
	public void A_page_reaching_past_a_bar_completes_it_and_fills_the_one_before()
	{
		using var footprint = Create();

		// 13:31:10 … 13:32:29: covers the 13:32 bar and the end of the 13:31 one
		footprint.AddPage(
			[Tick(70, 10.00), Tick(119, 10.00), Tick(120, 10.01), Tick(149, 10.00)],
			LiveFrom
		);

		Assert.Equal(FootprintBarState.Ready, footprint.GetState(T0.AddMinutes(2)));
		Assert.Equal(FootprintBarState.Partial, footprint.GetState(T0.AddMinutes(1)));
		var bar = footprint.Get(T0.AddMinutes(2))!;
		// 10.01 is an uptick from 13:31:59's 10.00 (the trade before the bar); 10.00 a downtick
		Assert.Equal((100m, 0m), (bar.Levels[1001].AskVolume, bar.Levels[1001].BidVolume));
		Assert.Equal((0m, 100m), (bar.Levels[1000].AskVolume, bar.Levels[1000].BidVolume));
		Assert.Equal(T0.AddMinutes(2), footprint.GetCompleteFrom());
	}

	[Fact]
	public void The_first_trades_of_a_page_take_their_side_once_the_trade_before_them_is_in()
	{
		using var footprint = Create();
		footprint.AddPage([Tick(130, 10.00), Tick(135, 10.00), Tick(140, 10.01)], LiveFrom);
		var level = footprint.Get(T0.AddMinutes(2))!.Levels[1000];
		// No trade before them yet: half on each side
		Assert.Equal((100m, 100m), (level.BidVolume, level.AskVolume));

		// 13:32:05's 9.99: 10.00 was an uptick
		footprint.AddPage([Tick(125, 9.99)], T0.AddSeconds(130));

		Assert.Equal((0m, 200m), (level.BidVolume, level.AskVolume));
		// 9.99 itself waits for the trade before it
		var bar = footprint.Get(T0.AddMinutes(2))!;
		Assert.Equal((50m, 350m), (bar.BidVolume, bar.AskVolume));
		Assert.Equal(FootprintBarState.Partial, footprint.GetState(T0.AddMinutes(2)));
	}

	[Fact]
	public void The_tick_rule_runs_across_pages_into_the_bar_before()
	{
		using var footprint = Create();
		footprint.AddPage([Tick(60, 10.02), Tick(130, 10.02)], LiveFrom);

		// The page before ends with 10.01 at 13:30:59: 13:31:00's 10.02 was an uptick
		footprint.AddPage([Tick(30, 10.00), Tick(59, 10.01)], T0.AddSeconds(60));

		var bar = footprint.Get(T0.AddMinutes(1))!;
		Assert.Equal(100m, bar.Levels[1002].AskVolume);
		Assert.Equal(FootprintBarState.Ready, footprint.GetState(T0.AddMinutes(1)));
		Assert.Equal(FootprintBarState.Partial, footprint.GetState(T0));
	}

	[Fact]
	public void The_backfill_stops_at_the_session_until_the_view_scrolls_back()
	{
		using var footprint = Create(sessionStart: T0.AddMinutes(1));
		footprint.SetVisible(T0.AddMinutes(1), T0.AddMinutes(2));

		footprint.AddPage([Tick(50, 10), Tick(149, 10)], LiveFrom);

		Assert.Null(footprint.GetNextPageEnd());
		Assert.Equal(FootprintBarState.Partial, footprint.GetState(T0));
		Assert.True(footprint.SetVisible(T0, T0.AddMinutes(2)));
		Assert.Equal(T0.AddSeconds(50), footprint.GetNextPageEnd());
	}

	[Fact]
	public void The_request_cap_stops_the_backfill_and_bars_without_trades_have_no_footprint()
	{
		using var footprint = Create(maxRequests: 2);

		footprint.AddPage([Tick(140, 10)], LiveFrom);
		footprint.AddPage([Tick(110, 10)], T0.AddMinutes(2));

		Assert.Null(footprint.GetNextPageEnd());
		Assert.True(footprint.IsStopped);
		Assert.Equal(FootprintBarState.Partial, footprint.GetState(T0.AddMinutes(1)));
		Assert.Equal(FootprintBarState.Unavailable, footprint.GetState(T0));
	}

	[Fact]
	public void At_the_start_of_ib_data_the_bars_left_have_no_footprint()
	{
		using var footprint = Create();
		footprint.AddPage([Tick(90, 10), Tick(149, 10.01)], LiveFrom);

		footprint.AddPage([], T0.AddSeconds(90));

		// The bar the oldest trade is in counts with what there is; the one before has nothing
		Assert.Equal(FootprintBarState.Ready, footprint.GetState(T0.AddMinutes(1)));
		Assert.Equal(FootprintBarState.Unavailable, footprint.GetState(T0));
		Assert.Null(footprint.GetNextPageEnd());
	}

	[Fact]
	public void Live_trades_count_from_where_the_pages_start()
	{
		using var footprint = Create();

		footprint.AddLiveTrade(Live(149, 10.00, FootprintSide.Bid));
		footprint.AddLiveTrade(Live(150, 10.00, FootprintSide.Bid));
		footprint.AddLiveTrade(Live(185, 10.02, FootprintSide.Ask));

		// 13:33 has no bar yet: its trades go to the bar to come
		Assert.Equal(10m, footprint.Get(T0.AddMinutes(3))!.AskVolume);
		// The one before LiveFrom is the backfill's
		Assert.Equal(10m, footprint.Get(T0.AddMinutes(2))!.BidVolume);
		footprint.AddPage([Tick(100, 10.00), Tick(130, 10.00)], LiveFrom);
		var forming = footprint.Get(T0.AddMinutes(2))!.Levels[1000];
		// 13:32:10 repeats 13:31:40's price with no side before it: half on each side
		Assert.Equal((60m, 50m), (forming.BidVolume, forming.AskVolume));
		Assert.Equal(FootprintBarState.Ready, footprint.GetState(T0.AddMinutes(2)));
	}

	[Fact]
	public void Bars_open_when_the_live_stream_failed_have_no_footprint()
	{
		using var footprint = Create();
		footprint.AddPage([Tick(10, 10), Tick(140, 10)], LiveFrom);
		footprint.AddPage([], T0.AddSeconds(10));

		footprint.SetLiveFailed(LiveFrom);

		Assert.Equal(FootprintBarState.Ready, footprint.GetState(T0.AddMinutes(1)));
		Assert.Equal(FootprintBarState.Unavailable, footprint.GetState(T0.AddMinutes(2)));
		Assert.Equal(FootprintBarState.Unavailable, footprint.GetState(T0.AddMinutes(3)));
	}

	private static FootprintTapeTradeDto Cached(
		int second,
		double price,
		FootprintSide quoteSide = FootprintSide.Unknown
	) => new(T0.AddSeconds(second).ToUnixTimeSeconds(), price, 100, quoteSide);

	private static FootprintTapeRangeDto Range(
		int from,
		int to,
		params FootprintTapeTradeDto[] trades
	) =>
		new()
		{
			From = T0.AddSeconds(from),
			To = T0.AddSeconds(to),
			Trades = trades,
		};

	[Fact]
	public void The_cached_day_comes_before_any_page()
	{
		using var footprint = Create();

		Assert.Equal(new DateTimeOffset(T0.Date, TimeSpan.Zero), footprint.GetNextRecordedDay());
		footprint.AddRecorded(new DateTimeOffset(T0.Date, TimeSpan.Zero), []);

		Assert.Null(footprint.GetNextRecordedDay());
		Assert.Equal(LiveFrom, footprint.GetNextPageEnd());
	}

	[Fact]
	public void Cached_trades_complete_the_bars_they_cover_without_a_page()
	{
		using var footprint = Create();

		// Every trade from 13:29:50 to LiveFrom
		footprint.AddRecorded(
			T0,
			[
				Range(
					-10,
					150,
					Cached(-5, 10.00),
					Cached(30, 10.01),
					Cached(90, 10.00, FootprintSide.Ask),
					Cached(140, 10.00)
				),
			]
		);

		Assert.Equal(FootprintBarState.Ready, footprint.GetState(T0));
		Assert.Equal(FootprintBarState.Ready, footprint.GetState(T0.AddMinutes(2)));
		Assert.Null(footprint.GetNextPageEnd());
		Assert.Equal(0, footprint.Requests);
		// 13:30:30 an uptick; 13:31:30 by its quote (at the ask), 13:32:20 keeping that side
		Assert.Equal(100m, footprint.Get(T0)!.Levels[1001].AskVolume);
		Assert.Equal(100m, footprint.Get(T0.AddMinutes(1))!.Levels[1000].AskVolume);
		Assert.Equal(100m, footprint.Get(T0.AddMinutes(2))!.Levels[1000].AskVolume);
	}

	// A recording that stopped at 13:32:05 and one that started at 13:32:10: IB is asked only
	// for what lies between, and the trade of the gap tells the side of the run after it
	[Fact]
	public void Pages_go_only_to_the_gaps_between_cached_stretches()
	{
		using var footprint = Create();
		footprint.AddRecorded(
			T0,
			[
				Range(-10, 125, Cached(-5, 10.00), Cached(70, 10.00)),
				Range(130, 150, Cached(135, 10.02)),
			]
		);

		Assert.Equal(FootprintBarState.Ready, footprint.GetState(T0.AddMinutes(1)));
		Assert.Equal(T0.AddSeconds(130), footprint.GetNextPageEnd());
		var level = footprint.Get(T0.AddMinutes(2))!.Levels[1002];
		Assert.Equal((50m, 50m), (level.BidVolume, level.AskVolume));

		footprint.AddPage([Tick(70, 10.00), Tick(127, 10.01)], T0.AddSeconds(130));

		// 10.02 after 10.01: an uptick
		Assert.Equal((0m, 100m), (level.BidVolume, level.AskVolume));
		Assert.Equal(FootprintBarState.Ready, footprint.GetState(T0.AddMinutes(2)));
		// 13:31:10 came from the cache, not again from the page
		Assert.Equal(100m, Volume(footprint.Get(T0.AddMinutes(1))!));
		Assert.Null(footprint.GetNextPageEnd());
		Assert.Equal(1, footprint.Requests);
	}

	[Fact]
	public void A_cached_stretch_counts_only_where_a_page_has_not_already()
	{
		using var footprint = Create();
		footprint.AddPage([Tick(100, 10), Tick(140, 10)], LiveFrom);

		footprint.AddRecorded(
			T0,
			[Range(90, 150, Cached(95, 10), Cached(100, 10), Cached(140, 10))]
		);

		Assert.Equal(
			300m,
			Volume(footprint.Get(T0.AddMinutes(1))!) + Volume(footprint.Get(T0.AddMinutes(2))!)
		);
	}

	private static decimal Volume(FootprintBarDto bar) => bar.BidVolume + bar.AskVolume;
}
