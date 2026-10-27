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
	public void Fitting_shows_every_bar_and_the_margin(int barCount, double margin)
	{
		var spacing = ChartViewportHelpers.GetFitBarSpacing(barCount, margin, PlotWidth, 40);
		var edge = ChartViewportHelpers.GetRightEdge(barCount, 0, margin);

		Assert.True(spacing <= 40);
		Assert.Equal(
			(0, barCount),
			ChartViewportHelpers.GetVisibleRange(barCount, edge, PlotWidth, spacing)
		);
		Assert.True(ChartViewportHelpers.GetBarX(0, edge, PlotRight, spacing) - spacing / 2 >= 0);
	}

	[Fact]
	public void Few_bars_are_fitted_no_wider_than_the_widest_zoom()
	{
		Assert.Equal(40, ChartViewportHelpers.GetFitBarSpacing(3, 0, PlotWidth, 40));
		Assert.Equal(40, ChartViewportHelpers.GetFitBarSpacing(0, 0, PlotWidth, 40));
	}

	[Fact]
	public void A_fitted_view_fits_again_when_the_plot_widens()
	{
		// A restored 5-minute Today chart at 12:35 (38 RTH bars, a margin of 5) is first laid out
		// 253 wide, then gets its real width; fitted once at 253 its bars filled only the right
		// part of the plot
		const int bars = 38;
		const double margin = 5;
		var first = ChartViewportHelpers.GetBarSpacing(8, true, bars, margin, 253, 40);

		var spacing = ChartViewportHelpers.GetBarSpacing(first, true, bars, margin, 534, 40);

		Assert.Equal(ChartViewportHelpers.GetFitBarSpacing(bars, margin, 534, 40), spacing);
		var edge = ChartViewportHelpers.GetRightEdge(bars, 0, margin);
		var left = ChartViewportHelpers.GetBarX(0, edge, 534, spacing) - spacing / 2;
		// The bars span the plot: only the spare slot is left of the first one
		Assert.InRange(left, 0, spacing * 1.001);
	}

	[Fact]
	public void A_fitted_view_fits_the_bars_that_arrive()
	{
		var first = ChartViewportHelpers.GetBarSpacing(8, true, 18, 0, PlotWidth, 40);

		var spacing = ChartViewportHelpers.GetBarSpacing(first, true, 200, 0, PlotWidth, 40);

		Assert.Equal(ChartViewportHelpers.GetFitBarSpacing(200, 0, PlotWidth, 40), spacing);
	}

	[Fact]
	public void A_zoomed_view_keeps_its_zoom_when_the_plot_is_resized()
	{
		Assert.Equal(12.5, ChartViewportHelpers.GetBarSpacing(12.5, false, 38, 5, 534, 40));
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
}
