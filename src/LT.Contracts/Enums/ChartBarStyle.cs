namespace LT.Contracts.Enums;

/// <summary>How a chart draws its bars. Saved per chart as an integer: add values at the end only.</summary>
public enum ChartBarStyle
{
	/// <summary>OHLC candles (the default).</summary>
	Candles = 0,

	/// <summary>
	/// Volume at each price, split into trades at the bid and at the ask, with imbalances and the
	/// bar's point of control, around a thin candle (intraday only).
	/// </summary>
	Footprint = 1,
}
