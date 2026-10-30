using System.Globalization;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

/// <summary>
/// Crosshair mode's math: where the vertical line snaps, the time label, and what the ruler
/// measures and says.
/// </summary>
public class ChartCrosshairHelpersTests
{
	private const double PlotRight = 800;
	private const double Spacing = 10;
	private static readonly TimeSpan FiveMinutes = TimeSpan.FromMinutes(5);
	private static readonly DateTimeOffset Open = new(2026, 10, 27, 13, 30, 0, TimeSpan.Zero);

	// 09:30, 09:35, 09:40, then the next morning (an overnight gap), 09:35
	private static readonly BarDto[] Bars =
	[
		new() { Time = Open },
		new() { Time = Open.AddMinutes(5) },
		new() { Time = Open.AddMinutes(10) },
		new() { Time = Open.AddDays(1) },
		new() { Time = Open.AddDays(1).AddMinutes(5) },
	];

	[Theory]
	// 100 bars, no margin: the last bar's slot is 790 … 800, its centre 795
	[InlineData(790.1, 795)]
	[InlineData(799.9, 795)]
	[InlineData(795, 795)]
	[InlineData(789.9, 785)]
	[InlineData(781, 785)]
	public void The_vertical_line_snaps_to_the_centre_of_the_bar_under_the_pointer(
		double x,
		double expected
	)
	{
		var edge = ChartViewportHelpers.GetRightEdge(100, 0, 0);

		Assert.Equal(expected, ChartCrosshairHelpers.GetSnappedX(x, edge, PlotRight, Spacing), 6);
	}

	[Fact]
	public void In_the_right_margin_the_line_snaps_to_the_empty_slots()
	{
		// 3 empty bars after the last one: the slots 770 … 800 are empty, 785 is the middle one's
		var edge = ChartViewportHelpers.GetRightEdge(100, 0, 3);

		Assert.Equal(785, ChartCrosshairHelpers.GetSnappedX(782, edge, PlotRight, Spacing), 6);
		// The snapped line is where the bar would be drawn
		Assert.Equal(
			ChartViewportHelpers.GetBarX(101, edge, PlotRight, Spacing),
			ChartCrosshairHelpers.GetSnappedX(782, edge, PlotRight, Spacing),
			6
		);
	}

	[Fact]
	public void The_intraday_time_label_has_the_weekday_date_and_time()
	{
		var time = new DateTimeOffset(2026, 10, 29, 14, 35, 0, TimeSpan.Zero);

		Assert.Equal(
			"Thu 29 Oct 26  14:35",
			ChartCrosshairHelpers.FormatTime(time, true, CultureInfo.InvariantCulture)
		);
	}

	[Fact]
	public void The_daily_time_label_has_only_the_date()
	{
		var time = new DateTimeOffset(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);

		Assert.Equal(
			"Thu 29 Oct 26",
			ChartCrosshairHelpers.FormatTime(time, false, CultureInfo.InvariantCulture)
		);
	}

	[Fact]
	public void The_ruler_measures_the_change_its_share_the_bars_and_the_time()
	{
		var measure = ChartCrosshairHelpers.Measure(
			Bars,
			FiveMinutes,
			Open,
			100,
			Open.AddMinutes(10),
			102.5
		);

		Assert.Equal(2.5, measure.Change, 9);
		Assert.Equal(2.5, measure.Percent!.Value, 9);
		Assert.Equal(2, measure.Bars);
		Assert.Equal(TimeSpan.FromMinutes(10), measure.Elapsed);
		Assert.True(measure.IsUp);
	}

	[Fact]
	public void Over_a_gap_the_ruler_counts_bars_and_the_whole_time()
	{
		// 09:35 one day to 09:35 the next: 3 bars apart, a day and 0 minutes of time
		var measure = ChartCrosshairHelpers.Measure(
			Bars,
			FiveMinutes,
			Open.AddMinutes(5),
			50,
			Open.AddDays(1).AddMinutes(5),
			50
		);

		Assert.Equal(3, measure.Bars);
		Assert.Equal(TimeSpan.FromDays(1), measure.Elapsed);
		// No change reads as up
		Assert.True(measure.IsUp);
	}

	[Fact]
	public void Dragged_down_and_left_the_ruler_is_negative()
	{
		var measure = ChartCrosshairHelpers.Measure(
			Bars,
			FiveMinutes,
			Open.AddMinutes(10),
			200,
			Open,
			190
		);

		Assert.Equal(-10, measure.Change, 9);
		Assert.Equal(-5, measure.Percent!.Value, 9);
		Assert.Equal(-2, measure.Bars);
		Assert.Equal(TimeSpan.FromMinutes(-10), measure.Elapsed);
		Assert.False(measure.IsUp);
	}

	[Fact]
	public void Past_the_latest_bar_the_ruler_counts_bars_at_the_bar_length()
	{
		var measure = ChartCrosshairHelpers.Measure(
			Bars,
			FiveMinutes,
			Bars[^1].Time,
			10,
			Bars[^1].Time.AddMinutes(15),
			11
		);

		Assert.Equal(3, measure.Bars);
		Assert.Equal(TimeSpan.FromMinutes(15), measure.Elapsed);
	}

	[Fact]
	public void A_start_at_zero_has_no_percentage()
	{
		var measure = ChartCrosshairHelpers.Measure(Bars, FiveMinutes, Open, 0, Open, 1);

		Assert.Null(measure.Percent);
		Assert.Equal(
			"+1.00\n0 bars, 0s",
			ChartCrosshairHelpers.GetLabel(measure, 2, CultureInfo.InvariantCulture)
		);
	}

	[Fact]
	public void The_label_says_the_change_the_percentage_the_bars_and_the_time()
	{
		var up = new ChartRulerMeasureDto
		{
			Change = 1.25,
			Percent = 0.8421,
			Bars = 12,
			Elapsed = TimeSpan.FromMinutes(60),
		};
		var down = new ChartRulerMeasureDto
		{
			Change = -0.5,
			Percent = -0.25,
			Bars = -1,
			Elapsed = TimeSpan.FromMinutes(-95),
		};

		Assert.Equal(
			"+1.25 (+0.84%)\n12 bars, 1h",
			ChartCrosshairHelpers.GetLabel(up, 2, CultureInfo.InvariantCulture)
		);
		Assert.Equal(
			"-0.500 (-0.25%)\n-1 bar, -1h 35m",
			ChartCrosshairHelpers.GetLabel(down, 3, CultureInfo.InvariantCulture)
		);
	}
}
