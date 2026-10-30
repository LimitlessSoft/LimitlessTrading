using LT.Contracts.Constants;

namespace LT.Contracts.Helpers;

/// <summary>
/// Horizontal viewport math of the candlestick chart. Positions are in bar units: bar
/// <c>i</c> spans <c>[i, i + 1)</c>, its centre at <c>i + 0.5</c>. The plot's right edge sits at
/// <see cref="GetRightEdge"/>: after the last bar, plus the right margin (empty bars after the
/// latest one), minus how many bars the view is panned back into history.
/// </summary>
public static class ChartViewportHelpers
{
	/// <summary>Bar position of the plot's right edge.</summary>
	public static double GetRightEdge(int barCount, int offset, double rightMargin) =>
		barCount + Math.Max(0, rightMargin) - offset;

	/// <summary>
	/// The smallest (most negative) offset: how far the view may be panned into the future, past
	/// the latest bar and the right margin, while
	/// <see cref="ChartConstants.MinVisibleBarsWhenPanned"/> bars (all, when fewer) stay visible
	/// at the plot's left edge. 0 when the plot or the zoom is unknown, or the plot can't be
	/// panned that far.
	/// </summary>
	public static int GetMinOffset(
		int barCount,
		double rightMargin,
		double plotWidth,
		double barSpacing
	)
	{
		if (barCount <= 0 || !(plotWidth > 0) || !(barSpacing > 0))
			return 0;
		var margin = double.IsNaN(rightMargin) ? 0 : Math.Max(0, rightMargin);
		var keep = Math.Min(ChartConstants.MinVisibleBarsWhenPanned, barCount);
		// The plot's left edge (right edge − plotWidth / barSpacing) at barCount − keep at most
		var min = Math.Ceiling(margin + keep - plotWidth / barSpacing);
		return min >= 0 ? 0 : (int)min;
	}

	/// <summary>
	/// <paramref name="offset"/> limited to <see cref="GetMinOffset"/> (panned into the future)
	/// … the oldest bar at the right edge (panned back into history).
	/// </summary>
	public static int ClampOffset(
		int offset,
		int barCount,
		double rightMargin,
		double plotWidth,
		double barSpacing
	) =>
		Math.Clamp(
			offset,
			GetMinOffset(barCount, rightMargin, plotWidth, barSpacing),
			Math.Max(0, barCount - 1)
		);

	/// <summary>
	/// Bar spacing after a wheel step: <paramref name="deltaY"/> &gt; 0 zooms in, &lt; 0 out (by
	/// <see cref="ChartConstants.WheelZoomFactor"/>), 0 (a horizontal swipe) leaves it. Never
	/// below <see cref="ChartConstants.MinBarSpacing"/> — or below the current spacing, when that
	/// is denser already — nor above <paramref name="maxSpacing"/>.
	/// </summary>
	public static double GetWheelBarSpacing(double barSpacing, double deltaY, double maxSpacing)
	{
		if (deltaY == 0 || double.IsNaN(deltaY))
			return barSpacing;
		var next =
			deltaY > 0
				? barSpacing * ChartConstants.WheelZoomFactor
				: barSpacing / ChartConstants.WheelZoomFactor;
		var min = Math.Min(ChartConstants.MinBarSpacing, barSpacing);
		return Math.Clamp(next, min, Math.Max(min, maxSpacing));
	}

	/// <summary>
	/// Bars whose centre lies inside a plot <paramref name="plotWidth"/> wide: <c>[Start, End)</c>.
	/// Empty (Start == End) when none do.
	/// </summary>
	public static (int Start, int End) GetVisibleRange(
		int barCount,
		double rightEdge,
		double plotWidth,
		double barSpacing
	)
	{
		var end = Math.Clamp((int)Math.Floor(rightEdge - 0.5) + 1, 0, barCount);
		var visible = Math.Max(1, (int)(plotWidth / barSpacing));
		var start = (int)Math.Ceiling(rightEdge - visible - 0.5);
		return (Math.Clamp(start, 0, Math.Max(0, end - 1)), end);
	}

	/// <summary>X of bar <paramref name="index"/>'s centre.</summary>
	public static double GetBarX(
		int index,
		double rightEdge,
		double plotRight,
		double barSpacing
	) => plotRight - (rightEdge - index - 0.5) * barSpacing;

	/// <summary>Index of the bar slot under <paramref name="x"/> (may be outside the bars).</summary>
	public static int GetBarIndexAt(
		double x,
		double rightEdge,
		double plotRight,
		double barSpacing
	) => (int)Math.Floor(rightEdge - (plotRight - x) / barSpacing);

	/// <summary>X of a bar position (fractional; bar <c>i</c>'s centre is position <c>i</c>).</summary>
	public static double GetPositionX(
		double position,
		double rightEdge,
		double plotRight,
		double barSpacing
	) => plotRight - (rightEdge - position - 0.5) * barSpacing;

	/// <summary>
	/// Bar position under <paramref name="x"/> (fractional; the inverse of
	/// <see cref="GetPositionX"/>). Rounded, it is the bar <see cref="GetBarIndexAt"/> gives.
	/// </summary>
	public static double GetPositionAt(
		double x,
		double rightEdge,
		double plotRight,
		double barSpacing
	) => rightEdge - 0.5 - (plotRight - x) / barSpacing;

	/// <summary>X of the line between the last bar and the right margin.</summary>
	public static double GetMarginHandleX(
		int barCount,
		double rightEdge,
		double plotRight,
		double barSpacing
	) => plotRight - (rightEdge - barCount) * barSpacing;

	/// <summary>
	/// The margin limited to 0 … <see cref="ChartConstants.MaxRightMarginFraction"/> of the plot
	/// width at the current zoom.
	/// </summary>
	public static double ClampRightMargin(double rightMargin, double plotWidth, double barSpacing)
	{
		if (double.IsNaN(rightMargin) || plotWidth <= 0 || barSpacing <= 0)
			return 0;
		var max = plotWidth * ChartConstants.MaxRightMarginFraction / barSpacing;
		return Math.Clamp(rightMargin, 0, max);
	}

	/// <summary>
	/// A view fitted to a period's window: the period's first bar <paramref name="firstIndex"/>
	/// at the plot's left edge and the window's <paramref name="windowSlots"/> bar slots across
	/// the plot (the slots still to come empty), half a slot to spare on each side. A window
	/// ending at the latest bar (<paramref name="windowSlots"/> null) holds the period's bars and
	/// the right margin. More bars than slots widen it to them. At most
	/// <paramref name="maxSpacing"/>: a window of few slots keeps its bars at the left, the space
	/// after the window empty. Returns the spacing and the plot's right edge (bar position).
	/// </summary>
	public static (double Spacing, double RightEdge) GetPeriodView(
		int firstIndex,
		int barCount,
		int? windowSlots,
		double rightMargin,
		double plotWidth,
		double maxSpacing
	)
	{
		var bars = Math.Max(0, barCount - firstIndex);
		var margin = double.IsNaN(rightMargin) ? 0 : Math.Max(0, rightMargin);
		var slots = windowSlots is { } window ? Math.Max(window, bars) : bars + margin;
		var spacing = plotWidth > 0 ? Math.Min(maxSpacing, plotWidth / (slots + 1)) : maxSpacing;
		return (spacing, firstIndex - 0.5 + (plotWidth > 0 ? plotWidth / spacing : slots + 1));
	}

	/// <summary>
	/// Whether to load the page of bars before the oldest: the view's first visible bar
	/// <paramref name="start"/> is within <see cref="ChartConstants.OlderBarsPrefetchScreens"/>
	/// of a screen (<paramref name="end"/> − <paramref name="start"/> bars) of the oldest, bar 0.
	/// </summary>
	public static bool NeedsOlderBars(int start, int end) =>
		end > start && start <= (end - start) * ChartConstants.OlderBarsPrefetchScreens;

	/// <summary>
	/// Margin while dragging its handle <paramref name="deltaX"/> DIPs from where the drag
	/// started: left (negative) widens it, right narrows it.
	/// </summary>
	public static double DragRightMargin(
		double startMargin,
		double deltaX,
		double plotWidth,
		double barSpacing
	) => ClampRightMargin(startMargin - deltaX / barSpacing, plotWidth, barSpacing);

	/// <summary>
	/// Width of the price axis for labels <paramref name="labelWidths"/> DIPs wide: the widest,
	/// <see cref="ChartConstants.PriceAxisPadding"/> on each side and the 1-DIP border at its left
	/// edge, rounded up to a whole DIP; at least <see cref="ChartConstants.MinPriceAxisWidth"/>.
	/// </summary>
	public static double GetPriceAxisWidth(IEnumerable<double> labelWidths)
	{
		var widest = labelWidths.Where(double.IsFinite).DefaultIfEmpty(0).Max();
		return Math.Max(
			ChartConstants.MinPriceAxisWidth,
			Math.Ceiling(widest + 2 * ChartConstants.PriceAxisPadding + 1)
		);
	}
}
