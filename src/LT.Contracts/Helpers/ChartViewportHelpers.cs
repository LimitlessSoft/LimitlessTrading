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
	/// Margin while dragging its handle <paramref name="deltaX"/> DIPs from where the drag
	/// started: left (negative) widens it, right narrows it.
	/// </summary>
	public static double DragRightMargin(
		double startMargin,
		double deltaX,
		double plotWidth,
		double barSpacing
	) => ClampRightMargin(startMargin - deltaX / barSpacing, plotWidth, barSpacing);
}
