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
}
