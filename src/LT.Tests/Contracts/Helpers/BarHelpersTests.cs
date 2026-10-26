using LT.Contracts.Dtos.Charts;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class BarHelpersTests
{
	private static readonly DateTimeOffset T0 = new(2026, 10, 23, 13, 30, 0, TimeSpan.Zero);

	private static BarDto Bar(
		DateTimeOffset time,
		double open,
		double high,
		double low,
		double close,
		decimal volume = 100
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

	[Fact]
	public void Update_inside_the_last_bar_extends_it()
	{
		var bars = new[] { Bar(T0, 10, 11, 9, 10.5) };

		var merged = BarHelpers.Merge(
			bars,
			Bar(T0.AddMinutes(2), 10.5, 12, 10.4, 11.8, 50),
			ChartTimeframe.FiveMinutes
		);

		var bar = Assert.Single(merged);
		Assert.Equal(
			(10d, 12d, 9d, 11.8d, 150m),
			(bar.Open, bar.High, bar.Low, bar.Close, bar.Volume)
		);
	}

	[Fact]
	public void Update_after_the_last_bar_starts_a_new_one_on_the_grid()
	{
		var bars = new[] { Bar(T0, 10, 11, 9, 10.5) };

		var merged = BarHelpers.Merge(
			bars,
			Bar(T0.AddMinutes(11).AddSeconds(5), 11, 11, 11, 11),
			ChartTimeframe.FiveMinutes
		);

		Assert.Equal(2, merged.Count);
		Assert.Equal(T0.AddMinutes(10), merged[1].Time);
	}

	[Fact]
	public void Daily_bars_update_in_place()
	{
		var bars = new[] { Bar(T0.Date, 10, 11, 9, 10.5) };

		var merged = BarHelpers.Merge(
			bars,
			Bar(T0.AddHours(30), 10.5, 13, 10, 12),
			ChartTimeframe.OneDay
		);

		Assert.Equal((13d, 12d), (Assert.Single(merged).High, merged[0].Close));
	}

	[Fact]
	public void Older_updates_are_ignored()
	{
		var bars = new[] { Bar(T0, 10, 11, 9, 10.5) };

		Assert.Same(
			bars,
			BarHelpers.Merge(bars, Bar(T0.AddMinutes(-5), 1, 1, 1, 1), ChartTimeframe.FiveMinutes)
		);
	}

	[Fact]
	public void A_price_inside_the_last_bar_moves_its_close_and_extends_high_and_low()
	{
		var bars = new[] { Bar(T0, 10, 11, 9, 10.5, 300) };

		var up = BarHelpers.MergePrice(bars, 12, T0.AddMinutes(1), ChartTimeframe.FiveMinutes);
		var down = BarHelpers.MergePrice(up, 8.5, T0.AddMinutes(2), ChartTimeframe.FiveMinutes);

		var bar = Assert.Single(down);
		Assert.Equal(
			(T0, 10d, 12d, 8.5d, 8.5d, 300m),
			(bar.Time, bar.Open, bar.High, bar.Low, bar.Close, bar.Volume)
		);
		Assert.Equal(10.5, bars[0].Close);
	}

	[Fact]
	public void A_price_past_the_last_bar_starts_a_new_one_on_the_grid_without_volume()
	{
		var bars = new[] { Bar(T0, 10, 11, 9, 10.5) };

		var merged = BarHelpers.MergePrice(
			bars,
			10.7,
			T0.AddMinutes(10).AddSeconds(2),
			ChartTimeframe.FiveMinutes
		);

		Assert.Equal(2, merged.Count);
		Assert.Same(bars[0], merged[0]);
		var bar = merged[1];
		Assert.Equal(
			(T0.AddMinutes(10), 10.7d, 10.7d, 10.7d, 10.7d, 0m),
			(bar.Time, bar.Open, bar.High, bar.Low, bar.Close, bar.Volume)
		);
	}

	[Fact]
	public void Daily_bars_take_prices_in_place()
	{
		var bars = new[] { Bar(T0.Date, 10, 11, 9, 10.5) };

		var merged = BarHelpers.MergePrice(bars, 11.5, T0.AddHours(30), ChartTimeframe.OneDay);

		Assert.Equal((11.5d, 11.5d), (Assert.Single(merged).High, merged[0].Close));
	}

	[Fact]
	public void Older_prices_unchanged_prices_and_empty_charts_change_nothing()
	{
		var bars = new[] { Bar(T0, 10, 11, 9, 10.5) };

		Assert.Same(
			bars,
			BarHelpers.MergePrice(bars, 12, T0.AddMinutes(-1), ChartTimeframe.FiveMinutes)
		);
		Assert.Same(
			bars,
			BarHelpers.MergePrice(bars, 10.5, T0.AddMinutes(1), ChartTimeframe.FiveMinutes)
		);
		Assert.Empty(BarHelpers.MergePrice([], 10, T0, ChartTimeframe.FiveMinutes));
	}

	[Fact]
	public void Volume_is_counted_once_when_prices_and_5_second_bars_both_move_a_bar()
	{
		var bars = new[] { Bar(T0, 10, 11, 9, 10.5, 300) };

		var merged = BarHelpers.MergePrice(bars, 11, T0.AddSeconds(61), ChartTimeframe.OneMinute);
		merged = BarHelpers.MergePrice(merged, 11.2, T0.AddSeconds(63), ChartTimeframe.OneMinute);
		merged = BarHelpers.Merge(
			merged,
			Bar(T0.AddSeconds(60), 11, 11.2, 11, 11.2, 40),
			ChartTimeframe.OneMinute
		);

		Assert.Equal([300m, 40m], merged.Select(x => x.Volume));
	}

	[Fact]
	public void Keep_close_adds_volume_and_range_but_leaves_a_newer_price_as_the_close()
	{
		var bars = new[] { Bar(T0, 10, 11, 9, 10.8, 300) };

		var merged = BarHelpers.Merge(
			bars,
			Bar(T0.AddMinutes(1), 10.5, 11.5, 10.4, 10.6, 50),
			ChartTimeframe.FiveMinutes,
			keepClose: true
		);

		var bar = Assert.Single(merged);
		Assert.Equal((11.5d, 9d, 10.8d, 350m), (bar.High, bar.Low, bar.Close, bar.Volume));
	}

	[Fact]
	public void A_5_second_bar_of_the_bar_before_a_price_started_one_is_folded_into_it()
	{
		// A trade at 13:31:01 started the 13:31 bar before the 13:30:55 5-second bar arrived
		var bars = new[]
		{
			Bar(T0, 10, 11, 9, 10.5, 300),
			Bar(T0.AddMinutes(1), 10.6, 10.6, 10.6, 10.6, 0),
		};

		var merged = BarHelpers.Merge(
			bars,
			Bar(T0.AddSeconds(55), 10.5, 11.4, 10.5, 10.6, 25),
			ChartTimeframe.OneMinute
		);

		Assert.Equal(2, merged.Count);
		Assert.Equal((11.4d, 10.6d, 325m), (merged[0].High, merged[0].Close, merged[0].Volume));
		Assert.Same(bars[1], merged[1]);
	}
}
