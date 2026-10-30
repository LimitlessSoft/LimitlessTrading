using System.Globalization;
using LT.Contracts.Dtos.Charts;

namespace LT.Contracts.Helpers;

/// <summary>
/// The chart's crosshair mode (F on a chart): where its vertical line snaps, the time it shows on
/// the time axis, and what its ruler measures. Bar positions are in bar units like
/// <see cref="ChartViewportHelpers"/> (bar <c>i</c>'s centre is position <c>i</c>).
/// </summary>
public static class ChartCrosshairHelpers
{
	/// <summary>
	/// X of the centre of the bar slot under <paramref name="x"/>, where the vertical line goes
	/// (slots before the first or after the latest bar too).
	/// </summary>
	public static double GetSnappedX(
		double x,
		double rightEdge,
		double plotRight,
		double barSpacing
	) =>
		ChartViewportHelpers.GetBarX(
			ChartViewportHelpers.GetBarIndexAt(x, rightEdge, plotRight, barSpacing),
			rightEdge,
			plotRight,
			barSpacing
		);

	/// <summary>
	/// The time label on the time axis: the weekday, the date and (intraday) the time, e.g.
	/// "Thu 29 Oct 26 14:35", or "Thu 29 Oct 26" for daily and weekly bars. The time is already
	/// in the zone it is shown in.
	/// </summary>
	public static string FormatTime(
		DateTimeOffset time,
		bool isIntraday,
		IFormatProvider? culture = null
	) =>
		time.ToString(
			isIntraday ? "ddd dd MMM yy  HH:mm" : "ddd dd MMM yy",
			culture ?? CultureInfo.CurrentCulture
		);

	/// <summary>
	/// What the ruler measures from <paramref name="from"/> to <paramref name="to"/> (each a
	/// time on the bars and a price): the price change and its share of the start price, the
	/// bars between them and the time between them.
	/// </summary>
	public static ChartRulerMeasureDto Measure(
		IReadOnlyList<BarDto> bars,
		TimeSpan barLength,
		DateTimeOffset fromTime,
		double fromPrice,
		DateTimeOffset toTime,
		double toPrice
	)
	{
		var change = toPrice - fromPrice;
		return new ChartRulerMeasureDto
		{
			Change = change,
			Percent = fromPrice != 0 ? change / Math.Abs(fromPrice) * 100 : null,
			Bars = (int)
				Math.Round(
					ChartDrawingHelpers.GetBarPosition(bars, toTime, barLength)
						- ChartDrawingHelpers.GetBarPosition(bars, fromTime, barLength)
				),
			Elapsed = toTime - fromTime,
		};
	}

	/// <summary>
	/// The ruler's label, two lines: the change and its percentage ("+1.25 (+0.84%)"), then the
	/// bars and the time ("12 bars, 1h").
	/// </summary>
	public static string GetLabel(
		ChartRulerMeasureDto measure,
		int decimals,
		IFormatProvider? culture = null
	)
	{
		culture ??= CultureInfo.CurrentCulture;
		var sign = measure.Change > 0 ? "+" : "";
		var price = sign + measure.Change.ToString("N" + decimals, culture);
		if (measure.Percent is { } percent)
			price += $" ({sign}{percent.ToString("N2", culture)}%)";
		var bars = $"{measure.Bars} {(Math.Abs(measure.Bars) == 1 ? "bar" : "bars")}";
		var time =
			(measure.Elapsed < TimeSpan.Zero ? "-" : "")
			+ ChartDrawingHelpers.FormatDuration(measure.Elapsed);
		return $"{price}\n{bars}, {time}";
	}
}
