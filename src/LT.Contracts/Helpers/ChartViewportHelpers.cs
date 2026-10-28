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
	/// Bar spacing that shows every bar and the right margin across a plot
	/// <paramref name="plotWidth"/> wide (with half a slot to spare on each side), at most
	/// <paramref name="maxSpacing"/>; <paramref name="maxSpacing"/> when there is nothing to fit.
	/// </summary>
	public static double GetFitBarSpacing(
		int barCount,
		double rightMargin,
		double plotWidth,
		double maxSpacing
	)
	{
		if (barCount <= 0 || !(plotWidth > 0))
			return maxSpacing;
		var slots = barCount + (rightMargin > 0 ? rightMargin : 0) + 1;
		return Math.Min(maxSpacing, plotWidth / slots);
	}

	/// <summary>
	/// Bar spacing for a render: a fitted view (<paramref name="fitted"/>) fits its latest
	/// <paramref name="fitCount"/> bars (the period's, from its start to now) and the right
	/// margin into the plot as it is now (<see cref="GetFitBarSpacing"/>), so it stays fitted when
	/// the plot is resized or bars arrive; otherwise, or with no bars to fit (a period without bars
	/// yet), the current zoom, <paramref name="barSpacing"/>.
	/// </summary>
	public static double GetBarSpacing(
		double barSpacing,
		bool fitted,
		int fitCount,
		double rightMargin,
		double plotWidth,
		double maxSpacing
	) =>
		fitted && fitCount > 0
			? GetFitBarSpacing(fitCount, rightMargin, plotWidth, maxSpacing)
			: barSpacing;

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
