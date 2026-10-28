using System.Globalization;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

/// <summary>
/// Chart drawings' math: points per kind, where a time sits among the bars (gaps, before the
/// first and after the last bar), Fibonacci levels, measure labels and the hit tests.
/// </summary>
public class ChartDrawingHelpersTests
{
	private static readonly (double, double, double, double) Bounds = (0, 0, 800, 400);
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
	[InlineData(ChartDrawingKind.TrendLine, 2)]
	[InlineData(ChartDrawingKind.HorizontalLine, 1)]
	[InlineData(ChartDrawingKind.VerticalLine, 1)]
	[InlineData(ChartDrawingKind.Text, 1)]
	[InlineData(ChartDrawingKind.ParallelChannel, 3)]
	[InlineData(ChartDrawingKind.Triangle, 3)]
	[InlineData(ChartDrawingKind.FibExtension, 3)]
	[InlineData(ChartDrawingKind.FibRetracement, 2)]
	[InlineData(ChartDrawingKind.DateRange, 2)]
	public void Each_kind_takes_its_number_of_points(ChartDrawingKind kind, int count)
	{
		Assert.Equal(count, ChartDrawingHelpers.GetPointCount(kind));
		Assert.True(ChartDrawingHelpers.HasValidPointCount(kind, count));
		Assert.False(ChartDrawingHelpers.HasValidPointCount(kind, count + 1));
	}

	[Fact]
	public void A_stroke_takes_two_points_up_to_the_limit()
	{
		Assert.False(ChartDrawingHelpers.HasValidPointCount(ChartDrawingKind.Brush, 1));
		Assert.True(ChartDrawingHelpers.HasValidPointCount(ChartDrawingKind.Brush, 2));
		Assert.True(
			ChartDrawingHelpers.HasValidPointCount(
				ChartDrawingKind.Brush,
				ChartDrawingConstants.MaxBrushPoints
			)
		);
		Assert.False(
			ChartDrawingHelpers.HasValidPointCount(
				ChartDrawingKind.Brush,
				ChartDrawingConstants.MaxBrushPoints + 1
			)
		);
		Assert.False(ChartDrawingHelpers.SnapsToBars(ChartDrawingKind.Brush));
		Assert.True(ChartDrawingHelpers.SnapsToBars(ChartDrawingKind.TrendLine));
	}

	[Fact]
	public void A_bar_time_is_its_index()
	{
		for (var i = 0; i < Bars.Length; i++)
		{
			Assert.Equal(i, ChartDrawingHelpers.GetBarPosition(Bars, Bars[i].Time, FiveMinutes));
			Assert.Equal(Bars[i].Time, ChartDrawingHelpers.GetTimeAt(Bars, i, FiveMinutes));
		}
	}

	[Fact]
	public void A_time_in_a_gap_is_interpolated_between_its_bars()
	{
		var midnight = Open.AddMinutes(10) + (Open.AddDays(1) - Open.AddMinutes(10)) / 2;

		Assert.Equal(2.5, ChartDrawingHelpers.GetBarPosition(Bars, midnight, FiveMinutes), 9);
		Assert.Equal(midnight, ChartDrawingHelpers.GetTimeAt(Bars, 2.5, FiveMinutes));
	}

	[Fact]
	public void Times_outside_the_bars_are_extrapolated_at_the_bar_length()
	{
		var future = Bars[^1].Time.AddMinutes(15);
		var past = Open.AddMinutes(-10);

		Assert.Equal(7, ChartDrawingHelpers.GetBarPosition(Bars, future, FiveMinutes), 9);
		Assert.Equal(-2, ChartDrawingHelpers.GetBarPosition(Bars, past, FiveMinutes), 9);
		Assert.Equal(future, ChartDrawingHelpers.GetTimeAt(Bars, 7, FiveMinutes));
		Assert.Equal(past, ChartDrawingHelpers.GetTimeAt(Bars, -2, FiveMinutes));
	}

	[Theory]
	[InlineData(-3.25)]
	[InlineData(0.4)]
	[InlineData(2.9)]
	[InlineData(3.7)]
	[InlineData(12.5)]
	public void A_position_round_trips_through_its_time(double position) =>
		Assert.Equal(
			position,
			ChartDrawingHelpers.GetBarPosition(
				Bars,
				ChartDrawingHelpers.GetTimeAt(Bars, position, FiveMinutes),
				FiveMinutes
			),
			6
		);

	[Fact]
	public void Older_bars_put_in_front_keep_a_drawing_on_its_time()
	{
		var time = Bars[3].Time;
		BarDto[] withOlder = [new() { Time = Open.AddDays(-1) }, .. Bars];

		Assert.Equal(3, ChartDrawingHelpers.GetBarPosition(Bars, time, FiveMinutes));
		Assert.Equal(4, ChartDrawingHelpers.GetBarPosition(withOlder, time, FiveMinutes));
	}

	[Fact]
	public void Retracement_levels_run_from_the_second_point_to_the_first()
	{
		// A move up from 100 to 200
		Assert.Equal(200, ChartDrawingHelpers.GetFibRetracementPrice(100, 200, 0));
		Assert.Equal(100, ChartDrawingHelpers.GetFibRetracementPrice(100, 200, 1));
		Assert.Equal(138.2, ChartDrawingHelpers.GetFibRetracementPrice(100, 200, 0.618), 9);
		Assert.Equal(150, ChartDrawingHelpers.GetFibRetracementPrice(100, 200, 0.5));
		Assert.Equal(38.2, ChartDrawingHelpers.GetFibRetracementPrice(100, 200, 1.618), 9);
	}

	[Fact]
	public void Extension_levels_project_the_move_from_the_third_point()
	{
		// Up 100 → 150, back to 120: 1 is 170, 1.618 is 200.9
		Assert.Equal(120, ChartDrawingHelpers.GetFibExtensionPrice(100, 150, 120, 0));
		Assert.Equal(170, ChartDrawingHelpers.GetFibExtensionPrice(100, 150, 120, 1));
		Assert.Equal(200.9, ChartDrawingHelpers.GetFibExtensionPrice(100, 150, 120, 1.618), 9);
	}

	[Fact]
	public void The_price_range_label_shows_the_change_and_its_percent()
	{
		var culture = CultureInfo.InvariantCulture;

		Assert.Equal(
			"+12.50 (+12.50%)",
			ChartDrawingHelpers.GetPriceRangeLabel(100, 112.5, 2, culture)
		);
		Assert.Equal(
			"-25.0 (-20.00%)",
			ChartDrawingHelpers.GetPriceRangeLabel(125, 100, 1, culture)
		);
		Assert.Equal("+1.00", ChartDrawingHelpers.GetPriceRangeLabel(0, 1, 2, culture));
	}

	[Fact]
	public void The_date_range_label_counts_bars_and_time()
	{
		Assert.Equal(
			"12 bars, 1h",
			ChartDrawingHelpers.GetDateRangeLabel(3, 15, Open, Open.AddHours(1))
		);
		Assert.Equal(
			"1 bar, 5m",
			ChartDrawingHelpers.GetDateRangeLabel(0, 1, Open, Open.AddMinutes(5))
		);
		Assert.Equal(
			"-2 bars, -1d 2h",
			ChartDrawingHelpers.GetDateRangeLabel(4, 2, Open.AddHours(26), Open)
		);
	}

	[Theory]
	[InlineData(30, "30s")]
	[InlineData(45 * 60, "45m")]
	[InlineData(90 * 60, "1h 30m")]
	[InlineData(3 * 86400, "3d")]
	[InlineData(3 * 86400 + 4 * 3600, "3d 4h")]
	public void Durations_read_short(int seconds, string expected) =>
		Assert.Equal(expected, ChartDrawingHelpers.FormatDuration(TimeSpan.FromSeconds(seconds)));

	[Fact]
	public void The_distance_to_a_segment_is_to_its_nearest_point()
	{
		Assert.Equal(5, ChartDrawingHelpers.DistanceToSegment((5, 5), (0, 0), (10, 0)));
		Assert.Equal(5, ChartDrawingHelpers.DistanceToSegment((13, 4), (0, 0), (10, 0)));
		Assert.Equal(5, ChartDrawingHelpers.DistanceToSegment((3, 4), (0, 0), (0, 0)));
	}

	[Fact]
	public void A_ray_runs_on_past_its_second_point_to_the_edge()
	{
		var line = ChartDrawingHelpers.ClipLine((100, 300), (200, 200), false, true, Bounds);

		Assert.NotNull(line);
		Assert.Equal((100, 300), line.Value.Start);
		Assert.Equal(400, line.Value.End.X, 9);
		Assert.Equal(0, line.Value.End.Y, 9);
	}

	[Fact]
	public void An_extended_line_reaches_both_edges()
	{
		var line = ChartDrawingHelpers.ClipLine((100, 200), (200, 200), true, true, Bounds);

		Assert.Equal(((0d, 200d), (800d, 200d)), line);
	}

	[Fact]
	public void A_vertical_extended_line_reaches_top_and_bottom()
	{
		var line = ChartDrawingHelpers.ClipLine((300, 100), (300, 150), true, true, Bounds);

		Assert.Equal(((300d, 0d), (300d, 400d)), line);
	}

	[Fact]
	public void A_line_off_the_bounds_is_not_drawn() =>
		Assert.Null(ChartDrawingHelpers.ClipLine((900, 100), (900, 150), true, true, Bounds));

	[Fact]
	public void Lines_are_hit_near_them_and_a_ray_only_on_its_side()
	{
		Assert.True(Hit(ChartDrawingKind.TrendLine, [(100, 100), (200, 200)], (150, 153)));
		Assert.False(Hit(ChartDrawingKind.TrendLine, [(100, 100), (200, 200)], (250, 250)));
		Assert.True(Hit(ChartDrawingKind.Ray, [(100, 100), (200, 200)], (250, 250)));
		Assert.False(Hit(ChartDrawingKind.Ray, [(100, 100), (200, 200)], (50, 50)));
		Assert.True(Hit(ChartDrawingKind.ExtendedLine, [(100, 100), (200, 200)], (50, 50)));
		Assert.True(Hit(ChartDrawingKind.Arrow, [(100, 100), (200, 200)], (199, 199)));
	}

	[Fact]
	public void Horizontal_and_vertical_lines_are_hit_across_the_plot()
	{
		Assert.True(Hit(ChartDrawingKind.HorizontalLine, [(400, 120)], (10, 123)));
		Assert.False(Hit(ChartDrawingKind.HorizontalLine, [(400, 120)], (10, 130)));
		Assert.True(Hit(ChartDrawingKind.HorizontalRay, [(400, 120)], (700, 120)));
		Assert.False(Hit(ChartDrawingKind.HorizontalRay, [(400, 120)], (300, 120)));
		Assert.True(Hit(ChartDrawingKind.VerticalLine, [(400, 120)], (402, 390)));
		Assert.False(Hit(ChartDrawingKind.VerticalLine, [(400, 120)], (420, 120)));
	}

	[Fact]
	public void Shapes_are_hit_inside_them()
	{
		Assert.True(Hit(ChartDrawingKind.Rectangle, [(100, 100), (200, 50)], (150, 75)));
		Assert.False(Hit(ChartDrawingKind.Rectangle, [(100, 100), (200, 50)], (150, 120)));
		// The ellipse in the box 100…200 × 50…100: its centre, not the box's corner
		Assert.True(Hit(ChartDrawingKind.Ellipse, [(100, 100), (200, 50)], (150, 75)));
		Assert.False(Hit(ChartDrawingKind.Ellipse, [(100, 100), (200, 50)], (102, 52)));
		Assert.True(
			Hit(ChartDrawingKind.Triangle, [(100, 200), (200, 200), (150, 100)], (150, 180))
		);
		Assert.False(
			Hit(ChartDrawingKind.Triangle, [(100, 200), (200, 200), (150, 100)], (110, 110))
		);
		Assert.True(Hit(ChartDrawingKind.PriceRange, [(100, 100), (200, 50)], (198, 52)));
		Assert.True(Hit(ChartDrawingKind.DateRange, [(100, 100), (200, 50)], (101, 99)));
	}

	[Fact]
	public void A_channel_is_hit_on_both_lines_and_between_them()
	{
		// Trend line 100,200 → 300,100; its parallel 50 below, through 200,200
		(double, double)[] channel = [(100, 200), (300, 100), (200, 200)];

		Assert.Equal(50, ChartDrawingHelpers.GetChannelOffset(channel[0], channel[1], channel[2]));
		Assert.True(Hit(ChartDrawingKind.ParallelChannel, channel, (200, 150)));
		Assert.True(Hit(ChartDrawingKind.ParallelChannel, channel, (300, 150)));
		Assert.True(Hit(ChartDrawingKind.ParallelChannel, channel, (200, 175)));
		Assert.False(Hit(ChartDrawingKind.ParallelChannel, channel, (200, 230)));
	}

	[Fact]
	public void Fib_retracement_is_hit_on_its_levels_and_between_0_and_1()
	{
		// Levels from y 300 (1) to y 100 (0); 1.618 at 100 + 1.618 × 200 = 423.6 → off; 0.5 at 200
		(double, double)[] fib = [(100, 300), (300, 100)];

		Assert.True(Hit(ChartDrawingKind.FibRetracement, fib, (250, 200)));
		Assert.True(Hit(ChartDrawingKind.FibRetracement, fib, (150, 120)));
		Assert.False(Hit(ChartDrawingKind.FibRetracement, fib, (400, 200)));
	}

	[Fact]
	public void Fib_extension_is_hit_on_its_levels_right_of_the_third_point()
	{
		// Up 100 px (y 300 → 200), back to y 260: level 1 at y 160, from x 300 to 400
		(double, double)[] fib = [(100, 300), (200, 200), (300, 260)];

		Assert.Equal((300, 400), ChartDrawingHelpers.GetFibExtensionSpan(fib[0], fib[1], fib[2]));
		Assert.True(Hit(ChartDrawingKind.FibExtension, fib, (350, 160)));
		Assert.False(Hit(ChartDrawingKind.FibExtension, fib, (350, 180)));
		Assert.False(Hit(ChartDrawingKind.FibExtension, fib, (450, 160)));
		Assert.True(Hit(ChartDrawingKind.FibExtension, fib, (150, 250)));
	}

	[Fact]
	public void A_stroke_is_hit_along_it_and_a_note_never_here()
	{
		(double, double)[] stroke = [(100, 100), (150, 120), (200, 100)];

		Assert.True(Hit(ChartDrawingKind.Brush, stroke, (175, 112)));
		Assert.False(Hit(ChartDrawingKind.Brush, stroke, (150, 100)));
		Assert.False(Hit(ChartDrawingKind.Text, [(100, 100)], (100, 100)));
	}

	[Fact]
	public void The_last_anchor_near_the_point_is_hit()
	{
		(double, double)[] anchors = [(100, 100), (103, 100), (300, 300)];

		Assert.Equal(1, ChartDrawingHelpers.HitAnchor(anchors, (101, 101), 5));
		Assert.Equal(2, ChartDrawingHelpers.HitAnchor(anchors, (300, 304), 5));
		Assert.Equal(-1, ChartDrawingHelpers.HitAnchor(anchors, (200, 200), 5));
	}

	[Fact]
	public void A_stroke_shows_only_its_ends_to_drag()
	{
		Assert.Equal([0, 4], ChartDrawingHelpers.GetEditableAnchors(ChartDrawingKind.Brush, 5));
		Assert.Equal(
			[0, 1, 2],
			ChartDrawingHelpers.GetEditableAnchors(ChartDrawingKind.Triangle, 3)
		);
	}

	[Fact]
	public void The_arrow_head_sits_behind_its_tip_on_both_sides()
	{
		var (left, right) = ChartDrawingHelpers.GetArrowHead((0, 0), (100, 0), 10);

		Assert.True(left.X < 100 && right.X < 100);
		Assert.Equal(left.X, right.X, 9);
		Assert.Equal(-left.Y, right.Y, 9);
		Assert.Equal(10, Math.Sqrt(Math.Pow(100 - left.X, 2) + left.Y * left.Y), 9);
	}

	[Fact]
	public void A_stroke_drops_points_too_close_together_but_keeps_its_end()
	{
		(double, double)[] points = [(0, 0), (0.5, 0), (1, 0), (3, 0), (3.5, 0)];

		Assert.Equal(
			[(0d, 0d), (3d, 0d), (3.5d, 0d)],
			ChartDrawingHelpers.SimplifyStroke(points, 2)
		);
	}

	[Fact]
	public void A_stroke_keeps_at_most_the_limit_of_points()
	{
		var points = Enumerable
			.Range(0, ChartDrawingConstants.MaxBrushPoints + 50)
			.Select(i => ((double)i * 10, 0d))
			.ToArray();

		Assert.Equal(
			ChartDrawingConstants.MaxBrushPoints,
			ChartDrawingHelpers.SimplifyStroke(points, 2).Count
		);
	}

	private static bool Hit(
		ChartDrawingKind kind,
		(double X, double Y)[] points,
		(double X, double Y) point
	) =>
		ChartDrawingHelpers.HitTest(
			kind,
			points,
			point,
			Bounds,
			ChartDrawingConstants.HitTolerance
		);
}
