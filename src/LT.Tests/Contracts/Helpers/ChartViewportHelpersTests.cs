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
}
