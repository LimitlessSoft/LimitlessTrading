using LT.App.Widgets.Chart;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Enums;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Widgets;

/// <summary>5-second bars and live prices folded into a chart's bars, redrawn per interval.</summary>
public class ChartLiveBarsTests
{
	private static readonly DateTimeOffset T0 = new(2026, 10, 23, 13, 30, 0, TimeSpan.Zero);

	private readonly FakeTimeProvider _time = new(T0.AddSeconds(30));
	private readonly List<IReadOnlyList<BarDto>> _published = [];

	private ChartLiveBars Create(TimeSpan redrawInterval) =>
		new(
			[Bar(T0, 10, 10.5, 9, 10.2, 300)],
			ChartTimeframe.OneMinute,
			_time,
			redrawInterval,
			_published.Add,
			x => x()
		);

	private ChartPriceDto Price(double price) => new() { Time = _time.GetUtcNow(), Price = price };

	[Fact]
	public void Prices_wait_for_the_first_5_second_bar()
	{
		using var live = Create(TimeSpan.Zero);

		live.AddPrice(Price(12));
		Assert.Empty(_published);

		live.AddRealtimeBar(Bar(T0.AddSeconds(25), 10.5, 10.6, 10.5, 10.6, 20));
		_time.Advance(TimeSpan.FromSeconds(1));
		live.AddPrice(Price(12));

		Assert.Equal(2, _published.Count);
		var bar = Assert.Single(_published[^1]);
		Assert.Equal((12d, 12d, 320m), (bar.High, bar.Close, bar.Volume));
	}

	[Fact]
	public void Prices_stop_moving_the_chart_when_5_second_bars_stop()
	{
		using var live = Create(TimeSpan.Zero);
		live.AddRealtimeBar(Bar(T0.AddSeconds(25), 10.5, 10.6, 10.5, 10.6, 20));

		_time.Advance(TimeSpan.FromMinutes(1));
		live.AddPrice(Price(12));

		Assert.Single(_published);
		Assert.Equal(10.6, live.Bars[^1].Close);
	}

	[Fact]
	public void A_5_second_bar_that_ended_before_the_newest_price_keeps_its_close()
	{
		using var live = Create(TimeSpan.Zero);
		live.AddRealtimeBar(Bar(T0.AddSeconds(20), 10.5, 10.6, 10.5, 10.6, 20));
		_time.Advance(TimeSpan.FromSeconds(2));
		live.AddPrice(Price(10.9));

		// The 13:30:25 bar arrives at 13:30:32, after the 10.9 trade
		live.AddRealtimeBar(Bar(T0.AddSeconds(25), 10.6, 10.8, 10.6, 10.7, 15));

		var bar = Assert.Single(live.Bars);
		Assert.Equal((10.9d, 10.9d, 335m), (bar.High, bar.Close, bar.Volume));
	}

	[Fact]
	public void A_tick_by_tick_price_stamped_by_ib_counts_on_the_apps_clock()
	{
		using var live = Create(TimeSpan.Zero);
		live.AddRealtimeBar(Bar(T0.AddSeconds(25), 10.5, 10.6, 10.5, 10.6, 20));
		_time.Advance(TimeSpan.FromSeconds(1));

		// IB's whole-second stamp, behind the app's clock
		live.AddPrice(new ChartPriceDto { Time = T0.AddSeconds(29), Price = 10.8 });

		Assert.Equal(10.8, live.Bars[^1].Close);
	}

	[Fact]
	public void Throttled_updates_are_redrawn_at_most_once_per_interval()
	{
		using var live = Create(TimeSpan.FromSeconds(1));
		live.AddRealtimeBar(Bar(T0.AddSeconds(25), 10.5, 10.6, 10.5, 10.6, 20));

		live.AddPrice(Price(10.7));
		live.AddPrice(Price(10.8));
		live.AddPrice(Price(10.9));
		Assert.Single(_published);

		_time.Advance(TimeSpan.FromSeconds(1));

		Assert.Equal(2, _published.Count);
		Assert.Equal((10.9d, 10.9d), (_published[^1][^1].High, _published[^1][^1].Close));
	}

	[Fact]
	public void Nothing_is_published_once_disposed()
	{
		var live = Create(TimeSpan.FromSeconds(1));
		live.AddRealtimeBar(Bar(T0.AddSeconds(25), 10.5, 10.6, 10.5, 10.6, 20));
		live.AddPrice(Price(11));

		live.Dispose();
		_time.Advance(TimeSpan.FromSeconds(2));
		live.AddPrice(Price(12));

		Assert.Single(_published);
	}

	private static BarDto Bar(
		DateTimeOffset time,
		double open,
		double high,
		double low,
		double close,
		decimal volume
	) =>
		new()
		{
			Time = time,
			Open = open,
			High = high,
			Low = low,
			Close = close,
			Volume = volume,
		};
}
