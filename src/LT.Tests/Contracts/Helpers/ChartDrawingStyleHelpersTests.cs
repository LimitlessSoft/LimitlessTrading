using LT.Contracts.Constants;
using LT.Contracts.Dtos.ChartDrawings;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

/// <summary>
/// The drawing extras' math: the magnet's snap to bar OHLC, where a pasted copy lands, the
/// Fibonacci level model (defaults, parsing, checks) and the style checks.
/// </summary>
public class ChartDrawingStyleHelpersTests
{
	private static readonly TimeSpan FiveMinutes = TimeSpan.FromMinutes(5);
	private static readonly DateTimeOffset Open = new(2026, 10, 27, 13, 30, 0, TimeSpan.Zero);

	// 09:30, 09:35, 09:40, then the next morning (an overnight gap), 09:35
	private static readonly BarDto[] Bars =
	[
		new()
		{
			Time = Open,
			Open = 100,
			High = 104,
			Low = 99,
			Close = 103,
		},
		new()
		{
			Time = Open.AddMinutes(5),
			Open = 103,
			High = 110,
			Low = 102,
			Close = 108,
		},
		new()
		{
			Time = Open.AddMinutes(10),
			Open = 108,
			High = 109,
			Low = 101,
			Close = 102,
		},
		new() { Time = Open.AddDays(1) },
		new() { Time = Open.AddDays(1).AddMinutes(5) },
	];

	// ---- magnet ------------------------------------------------------------------------------

	[Theory]
	[InlineData(1, 111, 110)] // above the bar: its high
	[InlineData(1, 107, 108)] // nearest the close
	[InlineData(1, 104, 103)] // nearest the open
	[InlineData(1, 90, 102)] // below the bar: its low
	[InlineData(0.4, 98, 99)] // rounds to bar 0: its low
	[InlineData(1.6, 101.4, 101)] // rounds to bar 2: its low
	public void The_magnet_snaps_to_the_nearest_ohlc_of_the_bar_under_the_point(
		double position,
		double price,
		double snapped
	) => Assert.Equal(snapped, ChartDrawingHelpers.GetMagnetPrice(Bars, position, price));

	[Theory]
	[InlineData(-1)]
	[InlineData(5)]
	[InlineData(double.NaN)]
	public void Off_the_bars_the_magnet_keeps_the_price(double position) =>
		Assert.Equal(123.4, ChartDrawingHelpers.GetMagnetPrice(Bars, position, 123.4));

	// ---- paste offset ------------------------------------------------------------------------

	[Fact]
	public void A_pasted_copy_moves_by_bars_through_the_gaps_and_by_price()
	{
		List<ChartDrawingPointDto> points =
		[
			new() { Time = Open.AddMinutes(5), Price = 100 },
			new() { Time = Open.AddMinutes(10), Price = 105 },
		];

		var moved = ChartDrawingHelpers.OffsetPoints(points, Bars, FiveMinutes, 2, -1.5);

		// Bar 1 → bar 3 (across the night), bar 2 → bar 4
		Assert.Equal([Open.AddDays(1), Open.AddDays(1).AddMinutes(5)], moved.Select(x => x.Time));
		Assert.Equal([98.5, 103.5], moved.Select(x => x.Price));
		// The copied drawing keeps its points
		Assert.Equal(Open.AddMinutes(5), points[0].Time);
	}

	[Fact]
	public void A_copy_pasted_past_the_last_bar_goes_on_at_the_bar_length()
	{
		var moved = ChartDrawingHelpers.OffsetPoints(
			[new ChartDrawingPointDto { Time = Open.AddDays(1).AddMinutes(5), Price = 50 }],
			Bars,
			FiveMinutes,
			3,
			0
		);

		Assert.Equal(Open.AddDays(1).AddMinutes(20), Assert.Single(moved).Time);
	}

	[Fact]
	public void Without_bars_a_copy_moves_at_the_bar_length()
	{
		var moved = ChartDrawingHelpers.OffsetPoints(
			[new ChartDrawingPointDto { Time = Open, Price = 50 }],
			[],
			FiveMinutes,
			3,
			1
		);

		Assert.Equal(Open.AddMinutes(15), moved[0].Time);
		Assert.Equal(51, moved[0].Price);
	}

	// ---- Fibonacci levels --------------------------------------------------------------------

	[Fact]
	public void The_default_levels_are_the_kind_s_each_in_its_colour()
	{
		var retracement = ChartDrawingHelpers.GetDefaultFibLevels(ChartDrawingKind.FibRetracement);
		var extension = ChartDrawingHelpers.GetDefaultFibLevels(ChartDrawingKind.FibExtension);

		Assert.Equal(ChartDrawingConstants.FibRetracementLevels, retracement.Select(x => x.Value));
		Assert.Equal(ChartDrawingConstants.FibExtensionLevels, extension.Select(x => x.Value));
		Assert.All(retracement, x => Assert.True(x.IsVisible));
		Assert.Equal(ChartDrawingConstants.FibColors, retracement.Select(x => x.Color));
		Assert.Empty(ChartDrawingHelpers.GetDefaultFibLevels(ChartDrawingKind.TrendLine));
	}

	[Theory]
	[InlineData("0.618", 0.618)]
	[InlineData(" 0,65 ", 0.65)]
	[InlineData("61.8%", 0.618)]
	[InlineData("-0.272", -0.272)]
	[InlineData("2.618", 2.618)]
	public void A_typed_level_is_read_as_a_fraction(string text, double value)
	{
		Assert.True(ChartDrawingHelpers.TryParseFibLevel(text, out var parsed));
		Assert.Equal(value, parsed, 9);
	}

	[Theory]
	[InlineData("")]
	[InlineData("abc")]
	[InlineData("%")]
	[InlineData(null)]
	public void Not_a_number_is_not_a_level(string? text) =>
		Assert.False(ChartDrawingHelpers.TryParseFibLevel(text, out _));

	[Fact]
	public void Levels_are_checked_for_range_and_repeats()
	{
		Assert.True(ChartDrawingHelpers.IsValidFibLevel(ChartDrawingConstants.MaxFibLevel));
		Assert.False(ChartDrawingHelpers.IsValidFibLevel(ChartDrawingConstants.MaxFibLevel + 0.1));
		Assert.False(ChartDrawingHelpers.IsValidFibLevel(double.PositiveInfinity));
		Assert.True(
			ChartDrawingHelpers.HasUniqueFibLevels([new() { Value = 0.5 }, new() { Value = 0.618 }])
		);
		Assert.False(
			ChartDrawingHelpers.HasUniqueFibLevels([
				new() { Value = 0.5 },
				new() { Value = 0.5000000001 },
			])
		);
	}

	// ---- style -------------------------------------------------------------------------------

	[Theory]
	[InlineData("#2962FF", true)]
	[InlineData("#802962ff", true)]
	[InlineData("2962FF", true)]
	[InlineData(" #2962FF ", true)]
	[InlineData("#2962F", false)]
	[InlineData("blue", false)]
	[InlineData("", false)]
	[InlineData(null, false)]
	public void A_colour_is_six_or_eight_hex_digits(string? color, bool valid) =>
		Assert.Equal(valid, ChartDrawingHelpers.IsValidColor(color));

	[Fact]
	public void A_colour_is_kept_upper_case_with_its_hash() =>
		Assert.Equal("#2962FF", ChartDrawingHelpers.NormalizeColor(" 2962ff "));

	[Theory]
	[InlineData(1, true)]
	[InlineData(4, true)]
	[InlineData(0.5, false)]
	[InlineData(4.5, false)]
	[InlineData(double.NaN, false)]
	public void A_line_width_is_in_range(double width, bool valid) =>
		Assert.Equal(valid, ChartDrawingHelpers.IsValidLineWidth(width));

	[Fact]
	public void The_built_in_style_is_the_old_look()
	{
		var line = ChartDrawingHelpers.GetBuiltInStyle(ChartDrawingKind.TrendLine);
		var note = ChartDrawingHelpers.GetBuiltInStyle(ChartDrawingKind.Text);
		var fib = ChartDrawingHelpers.GetBuiltInStyle(ChartDrawingKind.FibRetracement);

		Assert.Equal(ChartDrawingConstants.DefaultColor, line.Color);
		Assert.Equal(ChartDrawingLineStyle.Solid, line.LineStyle);
		Assert.Equal(1.5, line.LineWidth);
		Assert.Equal(ChartDrawingConstants.DefaultTextColor, note.Color);
		Assert.Equal(1, fib.LineWidth);
	}

	[Fact]
	public void Fib_hit_tests_use_the_drawing_s_levels()
	{
		(double, double, double, double) bounds = (0, 0, 800, 400);
		// The move 100 → 300 projected from y = 200: level 0 at 200, 1 at 400, a custom 0.1 at 220
		(double X, double Y)[] points = [(100, 100), (300, 300)];

		Assert.False(
			ChartDrawingHelpers.HitTest(
				ChartDrawingKind.FibExtension,
				[(100, 100), (300, 300), (400, 200)],
				(450, 220),
				bounds,
				3,
				[0, 1]
			)
		);
		Assert.True(
			ChartDrawingHelpers.HitTest(
				ChartDrawingKind.FibExtension,
				[(100, 100), (300, 300), (400, 200)],
				(450, 220),
				bounds,
				3,
				[0.1]
			)
		);
		Assert.True(
			ChartDrawingHelpers.HitTest(
				ChartDrawingKind.FibRetracement,
				points,
				(200, 150),
				bounds,
				3
			)
		);
	}
}
