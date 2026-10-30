using LT.Contracts.Constants;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>Decisions behind a chart's live updates (<see cref="ChartLiveUpdateMode"/>).</summary>
public static class ChartLiveUpdateHelpers
{
	/// <summary>
	/// The mode of a chart of the contract <paramref name="conId"/>: its Ticker settings entry's
	/// when it has one, else the app-wide <see cref="AppSettingsDto.ChartLiveUpdateMode"/>.
	/// </summary>
	public static ChartLiveUpdateMode GetMode(AppSettingsDto settings, int? conId) =>
		TickerSettingsHelpers.Find(settings.TickerSettings, conId)?.ChartLiveUpdateMode
		?? settings.ChartLiveUpdateMode;

	/// <summary>Modes that move the last bar with live prices on top of the 5-second bars.</summary>
	public static bool UsesLivePrices(ChartLiveUpdateMode mode) =>
		mode != ChartLiveUpdateMode.RealtimeBars;

	/// <summary>Modes whose live prices come from IB tick-by-tick data.</summary>
	public static bool UsesTickByTick(ChartLiveUpdateMode mode) =>
		mode is ChartLiveUpdateMode.TickByTick or ChartLiveUpdateMode.TickByTickThrottled;

	/// <summary>
	/// What a chart falls back to when the mode's price stream fails: tick-by-tick → quotes (keeping
	/// the throttle), quotes → null (5-second bars only).
	/// </summary>
	public static ChartLiveUpdateMode? GetFallback(ChartLiveUpdateMode mode) =>
		mode switch
		{
			ChartLiveUpdateMode.TickByTick => ChartLiveUpdateMode.Quotes,
			ChartLiveUpdateMode.TickByTickThrottled => ChartLiveUpdateMode.QuotesThrottled,
			_ => null,
		};

	/// <summary>The live price source for the status line ("tick-by-tick", "quotes"); null for none.</summary>
	public static string? GetSourceName(ChartLiveUpdateMode mode) =>
		UsesTickByTick(mode) ? "tick-by-tick"
		: UsesLivePrices(mode) ? "quotes"
		: null;

	/// <summary>Forex has no trades: its tick-by-tick price is the midpoint.</summary>
	public static TickByTickType GetTickByTickType(string securityType) =>
		securityType == MarketDataConstants.ForexType
			? TickByTickType.MidPoint
			: TickByTickType.Last;

	/// <summary>Least time between two redraws of a chart; zero = redraw on every update.</summary>
	public static TimeSpan GetRedrawInterval(ChartLiveUpdateMode mode, int redrawIntervalMs) =>
		mode is ChartLiveUpdateMode.QuotesThrottled or ChartLiveUpdateMode.TickByTickThrottled
			? TimeSpan.FromMilliseconds(
				Math.Clamp(
					redrawIntervalMs,
					ChartConstants.RedrawIntervalMsMin,
					ChartConstants.RedrawIntervalMsMax
				)
			)
			: TimeSpan.Zero;

	/// <summary>
	/// The price a quote moves the chart to, or null when it brings nothing new: the last trade
	/// when a trade happened since <paramref name="previous"/> (price, size, volume or time
	/// changed), the bid / ask midpoint for forex (no trades, like
	/// <see cref="ChartTimeframeHelpers.GetWhatToShow"/>). Delayed quotes never move a live
	/// chart.
	/// </summary>
	public static double? GetLivePrice(QuoteDto quote, QuoteDto? previous, string securityType)
	{
		if (quote.IsDelayed)
			return null;
		if (securityType == MarketDataConstants.ForexType)
		{
			var midpoint = GetMidpoint(quote);
			return midpoint != null && midpoint != (previous != null ? GetMidpoint(previous) : null)
				? midpoint
				: null;
		}
		if (quote.Last is not { } last || last <= 0)
			return null;
		var traded =
			previous == null
			|| previous.Last != quote.Last
			|| previous.LastSize != quote.LastSize
			|| previous.Volume != quote.Volume
			|| previous.LastTime != quote.LastTime;
		return traded ? last : null;
	}

	/// <summary>
	/// Live prices move the bars only while 5-second bars are flowing
	/// (<see cref="ChartConstants.LivePriceMaxBarAge"/>): not before the first one, and not once
	/// IB stops sending them (outside the chart's session, a failed stream).
	/// </summary>
	public static bool ShouldApplyPrice(DateTimeOffset now, DateTimeOffset? lastRealtimeBarAt) =>
		lastRealtimeBarAt is { } at && now - at <= ChartConstants.LivePriceMaxBarAge;

	/// <summary>
	/// A 5-second bar starting at <paramref name="barTime"/> keeps the chart's close when a live
	/// price applied after the 5-second bar ended is newer than the bar's own close.
	/// </summary>
	public static bool KeepsClose(DateTimeOffset? lastPriceAt, DateTimeOffset barTime) =>
		lastPriceAt is { } at && at >= barTime + ChartConstants.RealtimeBarLength;

	private static double? GetMidpoint(QuoteDto quote) =>
		quote is { Bid: > 0 and var bid, Ask: > 0 and var ask } ? (bid + ask) / 2 : null;
}
