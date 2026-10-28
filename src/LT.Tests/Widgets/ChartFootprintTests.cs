using LT.App.Widgets.Chart;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Widgets;

/// <summary>
/// A chart's footprints: past trades paged back newest first, a bar counted once a trade before
/// it is in, live trades from where the pages start, and what bars without trades show.
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

	private ChartFootprint Create(DateTimeOffset? backfillTo = null) =>
		new(
			ChartTimeframe.OneMinute,
			0.01,
			LiveFrom,
			backfillTo ?? T0,
			_time,
			() => _changes++,
			x => x()
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

		Assert.Equal(LiveFrom, footprint.GetNextPageEnd(40));
		Assert.Equal(FootprintBarState.Loading, footprint.GetState(T0));
	}

	[Fact]
	public void A_bar_counts_once_a_trade_before_its_start_is_in()
	{
		using var footprint = Create();

		// 13:31:10 … 13:32:29: covers the 13:32 bar, not yet the 13:31 one
		footprint.AddPage(
			[Tick(70, 10.00), Tick(119, 10.00), Tick(120, 10.01), Tick(149, 10.00)],
			_bars
		);

		Assert.Equal(FootprintBarState.Ready, footprint.GetState(T0.AddMinutes(2)));
		Assert.Equal(FootprintBarState.Loading, footprint.GetState(T0.AddMinutes(1)));
		Assert.Null(footprint.Get(T0.AddMinutes(1)));
		var bar = footprint.Get(T0.AddMinutes(2))!;
		// 10.01 is an uptick from 13:31:59's 10.00 (the trade before the bar); 10.00 a downtick
		Assert.Equal((100m, 0m), (bar.Levels[1001].AskVolume, bar.Levels[1001].BidVolume));
		Assert.Equal((0m, 100m), (bar.Levels[1000].AskVolume, bar.Levels[1000].BidVolume));
		Assert.Equal(T0.AddSeconds(70), footprint.GetNextPageEnd(40));
		Assert.True(_changes > 0);
	}

	[Fact]
	public void The_tick_rule_runs_across_pages()
	{
		using var footprint = Create();
		footprint.AddPage([Tick(60, 10.02), Tick(130, 10.02)], _bars);

		// The page before ends with 10.01 at 13:30:59: 13:31:00's 10.02 was an uptick
		footprint.AddPage([Tick(30, 10.00), Tick(59, 10.01)], _bars);

		var bar = footprint.Get(T0.AddMinutes(1))!;
		Assert.Equal(100m, bar.Levels[1002].AskVolume);
		Assert.Equal(FootprintBarState.Loading, footprint.GetState(T0));
	}

	[Fact]
	public void The_backfill_ends_at_its_target_and_the_rest_stays_without_a_footprint()
	{
		using var footprint = Create(backfillTo: T0.AddMinutes(1));

		footprint.AddPage([Tick(50, 10), Tick(149, 10)], _bars);

		Assert.Null(footprint.GetNextPageEnd(40));
		footprint.StopBackfill();
		Assert.Equal(FootprintBarState.Ready, footprint.GetState(T0.AddMinutes(1)));
		Assert.Equal(FootprintBarState.Unavailable, footprint.GetState(T0));
	}

	[Fact]
	public void The_request_cap_stops_the_backfill()
	{
		using var footprint = Create();

		footprint.AddPage([Tick(140, 10)], _bars);
		footprint.AddPage([Tick(130, 10)], _bars);

		Assert.Null(footprint.GetNextPageEnd(2));
		Assert.NotNull(footprint.GetNextPageEnd(3));
	}

	[Fact]
	public void At_the_start_of_ib_data_the_bars_left_have_no_footprint()
	{
		using var footprint = Create();
		footprint.AddPage([Tick(90, 10), Tick(149, 10.01)], _bars);

		footprint.AddPage([], _bars);

		Assert.Null(footprint.GetNextPageEnd(40));
		// The bar the oldest trade is in counts with what there is; the one before has nothing
		Assert.Equal(FootprintBarState.Ready, footprint.GetState(T0.AddMinutes(1)));
		Assert.Equal(FootprintBarState.Unavailable, footprint.GetState(T0));
	}

	[Fact]
	public void Live_trades_count_from_where_the_pages_start_and_new_bars_are_ready()
	{
		using var footprint = Create();

		footprint.AddLiveTrade(Live(149, 10.00, FootprintSide.Bid), () => _bars);
		footprint.AddLiveTrade(Live(150, 10.00, FootprintSide.Bid), () => _bars);
		footprint.AddLiveTrade(Live(185, 10.02, FootprintSide.Ask), () => _bars);

		// 13:33 has no bar yet: its trades go to the bar to come
		var next = footprint.Get(T0.AddMinutes(3))!;
		Assert.Equal(10m, next.AskVolume);
		// The forming bar waits for its past trades; the live one before LiveFrom is the backfill's
		Assert.Equal(FootprintBarState.Loading, footprint.GetState(T0.AddMinutes(2)));
		// 13:32:10 repeats 13:31:40's price with no side before it: half on each side
		footprint.AddPage([Tick(100, 10.00), Tick(130, 10.00)], _bars);
		var forming = footprint.Get(T0.AddMinutes(2))!.Levels[1000];
		Assert.Equal((60m, 50m), (forming.BidVolume, forming.AskVolume));
	}

	[Fact]
	public void Bars_open_when_the_live_stream_failed_have_no_footprint()
	{
		using var footprint = Create();
		footprint.AddPage([Tick(10, 10), Tick(140, 10)], _bars);
		footprint.AddPage([], _bars);

		footprint.SetLiveFailed(LiveFrom);

		Assert.Equal(FootprintBarState.Ready, footprint.GetState(T0.AddMinutes(1)));
		Assert.Equal(FootprintBarState.Unavailable, footprint.GetState(T0.AddMinutes(2)));
		Assert.Equal(FootprintBarState.Unavailable, footprint.GetState(T0.AddMinutes(3)));
	}
}
