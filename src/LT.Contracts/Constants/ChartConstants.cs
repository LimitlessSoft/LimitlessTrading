namespace LT.Contracts.Constants;

public static class ChartConstants
{
	/// <summary>The empty space after the last bar is at most this share of the plot width.</summary>
	public const double MaxRightMarginFraction = 0.5;

	/// <summary>Half-width (DIPs) of the margin handle's grab area in the time axis.</summary>
	public const double MarginHandleHitWidth = 5;

	/// <summary>Space (DIPs) on each side of the price axis labels.</summary>
	public const double PriceAxisPadding = 4;

	/// <summary>The price axis is never narrower than this (DIPs), however short its labels.</summary>
	public const double MinPriceAxisWidth = 32;

	/// <summary>An indicator pane's share of the chart height while there is room.</summary>
	public const double IndicatorPaneFraction = 0.2;

	/// <summary>All indicator panes together take at most this share; the price chart keeps the rest.</summary>
	public const double MaxIndicatorPanesFraction = 0.6;

	/// <summary>Space above and below the values of an autoscaled indicator pane.</summary>
	public const double IndicatorPanePadding = 0.08;

	/// <summary>Length of IB's real-time bars (<c>reqRealTimeBars</c> supports only 5 s).</summary>
	public static readonly TimeSpan RealtimeBarLength = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Live prices move the last bar only while a 5-second bar arrived this recently: IB sends one
	/// every 5 s while the chart's session is open (also without trades), so quotes outside it
	/// (extended hours on an RTH chart, a closed market) don't paint bars of their own.
	/// </summary>
	public static readonly TimeSpan LivePriceMaxBarAge = TimeSpan.FromSeconds(15);

	/// <summary>Chart redraw interval (the throttled chart live update modes), in ms.</summary>
	public const int RedrawIntervalMsMin = 250;
	public const int RedrawIntervalMsMax = 5000;
	public const int DefaultRedrawIntervalMs = 1000;
}
