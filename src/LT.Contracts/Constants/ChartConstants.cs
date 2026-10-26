namespace LT.Contracts.Constants;

public static class ChartConstants
{
	/// <summary>The empty space after the last bar is at most this share of the plot width.</summary>
	public const double MaxRightMarginFraction = 0.5;

	/// <summary>Half-width (DIPs) of the margin handle's grab area in the time axis.</summary>
	public const double MarginHandleHitWidth = 5;

	/// <summary>An indicator pane's share of the chart height while there is room.</summary>
	public const double IndicatorPaneFraction = 0.2;

	/// <summary>All indicator panes together take at most this share; the price chart keeps the rest.</summary>
	public const double MaxIndicatorPanesFraction = 0.6;

	/// <summary>Space above and below the values of an autoscaled indicator pane.</summary>
	public const double IndicatorPanePadding = 0.08;
}
