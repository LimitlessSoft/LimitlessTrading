namespace LT.Contracts.Enums;

/// <summary>
/// How an open chart follows the market after its history loads (Settings → General → Charts).
/// Persisted as an integer: add values at the end only.
/// </summary>
public enum ChartLiveUpdateMode
{
	/// <summary>IB's 5-second real-time bars only (the default).</summary>
	RealtimeBars = 0,

	/// <summary>
	/// 5-second bars plus every last-trade price from the quote stream (midpoint for forex)
	/// moving the last bar; one market data line per chart.
	/// </summary>
	Quotes = 1,

	/// <summary>As <see cref="Quotes"/>, redrawn at most once per chart redraw interval.</summary>
	QuotesThrottled = 2,

	/// <summary>
	/// 5-second bars plus IB tick-by-tick trades (<c>reqTickByTickData</c> Last, MidPoint for
	/// forex) moving the last bar, stamped by IB; one of IB's 5 tick-by-tick subscriptions per
	/// chart. Falls back to <see cref="Quotes"/> when none is left or IB refuses it.
	/// </summary>
	TickByTick = 3,

	/// <summary>As <see cref="TickByTick"/>, redrawn at most once per chart redraw interval.</summary>
	TickByTickThrottled = 4,
}
