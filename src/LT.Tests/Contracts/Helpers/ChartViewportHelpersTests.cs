using LT.Contracts.Constants;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class ChartViewportHelpersTests
{
	private const double PlotRight = 800;
	private const double PlotWidth = 800;
	private const double Spacing = 10;

	[Fact]
	public void Without_a_margin_the_last_bar_ends_at_the_plot_edge()
	{
		var edge = ChartViewportHelpers.GetRightEdge(100, 0, 0);

		Assert.Equal(PlotRight - 5, ChartViewportHelpers.GetBarX(99, edge, PlotRight, Spacing));
		Assert.Equal(
			(20, 100),
			ChartViewportHelpers.GetVisibleRange(100, edge, PlotWidth, Spacing)
		);
		Assert.Equal(
			PlotRight,
			ChartViewportHelpers.GetMarginHandleX(100, edge, PlotRight, Spacing)
		);
	}

	[Fact]
	public void A_margin_leaves_empty_bars_after_the_last_one()
	{
		var edge = ChartViewportHelpers.GetRightEdge(100, 0, 10);

		Assert.Equal(PlotRight - 105, ChartViewportHelpers.GetBarX(99, edge, PlotRight, Spacing));
		Assert.Equal(
			PlotRight - 100,
			ChartViewportHelpers.GetMarginHandleX(100, edge, PlotRight, Spacing)
		);
		// 80 slots, 10 of them empty: 70 bars
		Assert.Equal(
			(30, 100),
			ChartViewportHelpers.GetVisibleRange(100, edge, PlotWidth, Spacing)
		);
	}

	[Fact]
	public void A_new_bar_keeps_the_margin()
	{
		var before = ChartViewportHelpers.GetRightEdge(100, 0, 10);
		var after = ChartViewportHelpers.GetRightEdge(101, 0, 10);

		Assert.Equal(
			ChartViewportHelpers.GetBarX(99, before, PlotRight, Spacing),
			ChartViewportHelpers.GetBarX(100, after, PlotRight, Spacing)
		);
	}

	[Fact]
	public void Panning_into_history_first_uses_up_the_margin()
	{
		var edge = ChartViewportHelpers.GetRightEdge(100, 4, 10);

		Assert.Equal(
			PlotRight - 60,
			ChartViewportHelpers.GetMarginHandleX(100, edge, PlotRight, Spacing)
		);
		Assert.Equal(100, ChartViewportHelpers.GetVisibleRange(100, edge, PlotWidth, Spacing).End);

		var past = ChartViewportHelpers.GetRightEdge(100, 15, 10);
		Assert.Equal(95, ChartViewportHelpers.GetVisibleRange(100, past, PlotWidth, Spacing).End);
		Assert.True(
			ChartViewportHelpers.GetMarginHandleX(100, past, PlotRight, Spacing) > PlotRight
		);
	}

	[Fact]
	public void A_partial_margin_bar_does_not_hide_the_last_bar()
	{
		var edge = ChartViewportHelpers.GetRightEdge(100, 3, 2.4);

		// right edge at 99.4: bar 98's centre (98.5) is in, bar 99's (99.5) is not
		Assert.Equal(99, ChartViewportHelpers.GetVisibleRange(100, edge, PlotWidth, Spacing).End);
	}

	[Theory]
	[InlineData(PlotRight - 15, 99)]
	[InlineData(PlotRight - 55, 95)]
	[InlineData(PlotRight - 1, 100)] // in the margin
	public void The_slot_under_the_pointer_accounts_for_the_margin(double x, int expected)
	{
		var edge = ChartViewportHelpers.GetRightEdge(100, 0, 1);

		Assert.Equal(expected, ChartViewportHelpers.GetBarIndexAt(x, edge, PlotRight, Spacing));
	}

	[Theory]
	[InlineData(-3, 0)]
	[InlineData(12.5, 12.5)]
	[InlineData(100, 40)] // half of 800 DIPs at 10 per bar
	[InlineData(double.NaN, 0)]
	public void The_margin_stays_between_zero_and_half_the_plot(double margin, double expected)
	{
		Assert.Equal(expected, ChartViewportHelpers.ClampRightMargin(margin, PlotWidth, Spacing));
	}

	[Fact]
	public void The_margin_cap_follows_the_zoom()
	{
		Assert.Equal(20, ChartViewportHelpers.ClampRightMargin(100, PlotWidth, 20));
	}

	[Theory]
	[InlineData(-50, 15)] // dragged left: wider
	[InlineData(30, 7)] // dragged right: narrower
	[InlineData(200, 0)]
	[InlineData(-1000, 40)]
	public void Dragging_the_handle_sets_the_margin(double deltaX, double expected)
	{
		Assert.Equal(
			expected,
			ChartViewportHelpers.DragRightMargin(10, deltaX, PlotWidth, Spacing),
			6
		);
	}

	[Theory]
	[InlineData(100, 0)]
	[InlineData(4000, 0)] // a month of 5-minute bars: denser than the wheel goes
	[InlineData(100, 10)]
	[InlineData(1, 0)]
	public void A_window_ending_at_the_latest_bar_shows_every_period_bar_and_the_margin(
		int barCount,
		double margin
	)
	{
		var (spacing, edge) = ChartViewportHelpers.GetPeriodView(
			0,
			barCount,
			null,
			margin,
			PlotWidth,
			40
		);

		Assert.True(spacing <= 40);
		Assert.Equal(
			(0, barCount),
			ChartViewportHelpers.GetVisibleRange(barCount, edge, PlotWidth, spacing)
		);
		Assert.True(ChartViewportHelpers.GetBarX(0, edge, PlotRight, spacing) - spacing / 2 >= 0);
		Assert.True(
			ChartViewportHelpers.GetMarginHandleX(barCount, edge, PlotRight, spacing) <= PlotRight
		);
	}

	[Fact]
	public void The_periods_first_bar_is_at_the_left_edge_and_no_older_bar_is_in_view()
	{
		// 1,000 bars loaded, the period holds the latest 80: the bar before them was half in view
		// (the previous session's last candle at the far left)
		var (spacing, edge) = ChartViewportHelpers.GetPeriodView(920, 1000, null, 0, PlotWidth, 40);

		var left = ChartViewportHelpers.GetBarX(920, edge, PlotRight, spacing) - spacing / 2;
		Assert.Equal(spacing / 2, left, 6);
		Assert.True(ChartViewportHelpers.GetBarX(919, edge, PlotRight, spacing) <= 0);
	}

	[Fact]
	public void A_fixed_window_lays_out_all_its_slots_with_the_periods_few_bars_at_the_left()
	{
		// Today on 5-minute RTH bars two candles after the open: 78 slots, 2 bars, after a
		// thousand older ones that used to fill the rest of the plot
		var (spacing, edge) = ChartViewportHelpers.GetPeriodView(1000, 1002, 78, 0, PlotWidth, 40);

		Assert.Equal(PlotWidth / 79, spacing, 6);
		Assert.Equal(spacing, ChartViewportHelpers.GetBarX(1000, edge, PlotRight, spacing), 6);
		// The window's last slot ends half a slot before the plot's right edge
		Assert.Equal(1000 + 78 + 0.5, edge, 6);
	}

	[Fact]
	public void A_fixed_window_ignores_the_margin_and_widens_to_more_bars_than_slots()
	{
		Assert.Equal(
			ChartViewportHelpers.GetPeriodView(0, 2, 78, 0, PlotWidth, 40),
			ChartViewportHelpers.GetPeriodView(0, 2, 78, 25, PlotWidth, 40)
		);
		var (spacing, _) = ChartViewportHelpers.GetPeriodView(0, 100, 78, 0, PlotWidth, 40);
		Assert.Equal(PlotWidth / 101, spacing, 6);
	}

	[Fact]
	public void A_window_of_few_slots_is_no_wider_than_the_widest_zoom_and_stays_at_the_left()
	{
		// Today on daily bars: one slot
		var (spacing, edge) = ChartViewportHelpers.GetPeriodView(500, 501, 1, 0, PlotWidth, 40);

		Assert.Equal(40, spacing);
		Assert.Equal(spacing, ChartViewportHelpers.GetBarX(500, edge, PlotRight, spacing), 6);
	}

	[Fact]
	public void The_window_fits_again_when_the_plot_widens()
	{
		// A restored chart is first laid out 253 wide, then gets its real width
		var narrow = ChartViewportHelpers.GetPeriodView(0, 38, 78, 0, 253, 40);
		var wide = ChartViewportHelpers.GetPeriodView(0, 38, 78, 0, 534, 40);

		Assert.Equal(253 / 79.0, narrow.Spacing, 6);
		Assert.Equal(534 / 79.0, wide.Spacing, 6);
		Assert.Equal(narrow.RightEdge, wide.RightEdge, 6);
	}

	[Theory]
	[InlineData(0, 100, true)] // the oldest bar is on screen
	[InlineData(50, 150, true)] // half a screen away
	[InlineData(51, 151, false)]
	[InlineData(900, 1000, false)]
	[InlineData(0, 0, false)] // nothing shown
	public void Older_bars_are_needed_within_half_a_screen_of_the_oldest(
		int start,
		int end,
		bool needed
	)
	{
		Assert.Equal(needed, ChartViewportHelpers.NeedsOlderBars(start, end));
	}

	[Fact]
	public void The_price_axis_fits_its_widest_label_with_padding_and_border()
	{
		// 40.3 wide + 4 + 4 padding + 1 border = 49.3, rounded up
		Assert.Equal(50, ChartViewportHelpers.GetPriceAxisWidth([31.2, 40.3, 12]));
	}

	[Fact]
	public void The_price_axis_has_a_minimum_width()
	{
		Assert.Equal(ChartConstants.MinPriceAxisWidth, ChartViewportHelpers.GetPriceAxisWidth([6]));
		Assert.Equal(ChartConstants.MinPriceAxisWidth, ChartViewportHelpers.GetPriceAxisWidth([]));
		Assert.Equal(
			ChartConstants.MinPriceAxisWidth,
			ChartViewportHelpers.GetPriceAxisWidth([double.NaN])
		);
	}

	[Fact]
	public void A_bar_position_maps_to_its_bar_centre_and_back()
	{
		var edge = ChartViewportHelpers.GetRightEdge(100, 0, 0);

		Assert.Equal(
			ChartViewportHelpers.GetBarX(42, edge, PlotRight, Spacing),
			ChartViewportHelpers.GetPositionX(42, edge, PlotRight, Spacing)
		);
		Assert.Equal(
			42.3,
			ChartViewportHelpers.GetPositionAt(
				ChartViewportHelpers.GetPositionX(42.3, edge, PlotRight, Spacing),
				edge,
				PlotRight,
				Spacing
			),
			9
		);
	}

	[Theory]
	[InlineData(397)]
	[InlineData(403)]
	[InlineData(406)]
	public void A_rounded_position_is_the_bar_slot_under_the_point(double x)
	{
		var edge = ChartViewportHelpers.GetRightEdge(100, 0, 0);

		Assert.Equal(
			ChartViewportHelpers.GetBarIndexAt(x, edge, PlotRight, Spacing),
			(int)Math.Floor(ChartViewportHelpers.GetPositionAt(x, edge, PlotRight, Spacing) + 0.5)
		);
	}

	[Fact]
	public void Panning_into_the_future_stops_with_a_few_bars_left_at_the_left_edge()
	{
		// 80 slots: the right edge may go to 175, the left edge then at bar 95
		var min = ChartViewportHelpers.GetMinOffset(100, 0, PlotWidth, Spacing);
		var edge = ChartViewportHelpers.GetRightEdge(100, min, 0);

		Assert.Equal(-75, min);
		Assert.Equal(
			(100 - ChartConstants.MinVisibleBarsWhenPanned, 100),
			ChartViewportHelpers.GetVisibleRange(100, edge, PlotWidth, Spacing)
		);
		// Every slot after the last bar maps back to a slot past the bars
		Assert.Equal(
			174,
			ChartViewportHelpers.GetBarIndexAt(PlotRight - 1, edge, PlotRight, Spacing)
		);
	}

	[Fact]
	public void The_margin_counts_towards_the_future_pan()
	{
		var min = ChartViewportHelpers.GetMinOffset(100, 10, PlotWidth, Spacing);
		var edge = ChartViewportHelpers.GetRightEdge(100, min, 10);

		Assert.Equal(-65, min);
		Assert.Equal(
			(95, 100),
			ChartViewportHelpers.GetVisibleRange(100, edge, PlotWidth, Spacing)
		);
	}

	[Fact]
	public void Fewer_bars_than_kept_all_stay_visible_when_panned_into_the_future()
	{
		var min = ChartViewportHelpers.GetMinOffset(3, 0, PlotWidth, Spacing);
		var edge = ChartViewportHelpers.GetRightEdge(3, min, 0);

		Assert.Equal(-77, min);
		Assert.Equal((0, 3), ChartViewportHelpers.GetVisibleRange(3, edge, PlotWidth, Spacing));
	}

	[Theory]
	[InlineData(0, 800, 10)] // no bars
	[InlineData(100, 0, 10)] // not laid out yet
	[InlineData(100, 800, 0)]
	[InlineData(100, 30, 10)] // narrower than the bars kept
	public void Without_room_the_view_cannot_be_panned_into_the_future(
		int barCount,
		double plotWidth,
		double barSpacing
	) => Assert.Equal(0, ChartViewportHelpers.GetMinOffset(barCount, 0, plotWidth, barSpacing));

	[Theory]
	[InlineData(-1000, -75)]
	[InlineData(-20, -20)]
	[InlineData(0, 0)]
	[InlineData(50, 50)]
	[InlineData(1000, 99)]
	public void The_offset_is_clamped_between_the_future_and_the_oldest_bar(
		int offset,
		int expected
	) =>
		Assert.Equal(
			expected,
			ChartViewportHelpers.ClampOffset(offset, 100, 0, PlotWidth, Spacing)
		);

	[Fact]
	public void Zooming_out_goes_below_two_dips()
	{
		var spacing = ChartViewportHelpers.GetWheelBarSpacing(2, -1, 40);

		Assert.Equal(2 / ChartConstants.WheelZoomFactor, spacing, 10);
	}

	[Fact]
	public void Zooming_out_stops_at_the_minimum_spacing()
	{
		var spacing = 8.0;
		for (var i = 0; i < 100; i++)
			spacing = ChartViewportHelpers.GetWheelBarSpacing(spacing, -1, 40);

		Assert.Equal(ChartConstants.MinBarSpacing, spacing);
	}

	[Fact]
	public void A_view_denser_than_the_minimum_keeps_its_spacing_when_zoomed_out()
	{
		Assert.Equal(0.1, ChartViewportHelpers.GetWheelBarSpacing(0.1, -1, 40));
		Assert.Equal(
			0.1 * ChartConstants.WheelZoomFactor,
			ChartViewportHelpers.GetWheelBarSpacing(0.1, 1, 40),
			10
		);
	}

	[Fact]
	public void Zooming_in_stops_at_the_maximum_spacing() =>
		Assert.Equal(40, ChartViewportHelpers.GetWheelBarSpacing(39, 1, 40));

	[Fact]
	public void A_horizontal_swipe_does_not_zoom() =>
		Assert.Equal(5, ChartViewportHelpers.GetWheelBarSpacing(5, 0, 40));
}
