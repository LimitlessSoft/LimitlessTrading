using System.Globalization;
using LT.Contracts.Dtos.MarketData;

namespace LT.Contracts.Helpers;

/// <summary>
/// Folds IB ticks into a <see cref="QuoteDto"/>. Live and delayed tick types map to the same
/// fields (see IB "Available Tick Types").
/// </summary>
public static class TickTypeHelpers
{
	public const int BidSize = 0;
	public const int Bid = 1;
	public const int Ask = 2;
	public const int AskSize = 3;
	public const int Last = 4;
	public const int LastSize = 5;
	public const int High = 6;
	public const int Low = 7;
	public const int Volume = 8;
	public const int Close = 9;
	public const int AverageVolume = 21;
	public const int OptionImpliedVolatility = 24;
	public const int Open = 14;
	public const int LastTimestamp = 45;
	public const int Halted = 49;
	public const int DelayedBid = 66;
	public const int DelayedAsk = 67;
	public const int DelayedLast = 68;
	public const int DelayedBidSize = 69;
	public const int DelayedAskSize = 70;
	public const int DelayedLastSize = 71;
	public const int DelayedHigh = 72;
	public const int DelayedLow = 73;
	public const int DelayedVolume = 74;
	public const int DelayedClose = 75;
	public const int DelayedOpen = 76;
	public const int DelayedLastTimestamp = 88;

	/// <summary>Applies one tick; returns false when the tick type is not one we track.</summary>
	public static bool Apply(QuoteDto quote, MarketDataTickDto tick)
	{
		if (tick.Field is >= DelayedBid and <= DelayedOpen or DelayedLastTimestamp)
			quote.IsDelayed = true;

		var price = tick.Price is > 0 ? tick.Price : null;
		switch (tick.Field)
		{
			case Bid or DelayedBid:
				quote.Bid = price;
				return true;
			case Ask or DelayedAsk:
				quote.Ask = price;
				return true;
			case Last or DelayedLast:
				quote.Last = price;
				return true;
			case High or DelayedHigh:
				quote.High = price;
				return true;
			case Low or DelayedLow:
				quote.Low = price;
				return true;
			case Close or DelayedClose:
				quote.Close = price;
				return true;
			case Open or DelayedOpen:
				quote.Open = price;
				return true;
			case BidSize or DelayedBidSize:
				quote.BidSize = tick.Size;
				return true;
			case AskSize or DelayedAskSize:
				quote.AskSize = tick.Size;
				return true;
			case LastSize or DelayedLastSize:
				quote.LastSize = tick.Size;
				return true;
			case Volume or DelayedVolume:
				quote.Volume = tick.Size;
				return true;
			case AverageVolume:
				quote.AverageVolume = tick.Size is > 0 ? tick.Size : null;
				return true;
			case LastTimestamp or DelayedLastTimestamp:
				if (
					long.TryParse(
						tick.StringValue,
						NumberStyles.Integer,
						CultureInfo.InvariantCulture,
						out var seconds
					)
				)
					quote.LastTime = DateTimeOffset.FromUnixTimeSeconds(seconds);
				return true;
			case Halted:
				quote.IsHalted = tick.Value is > 0;
				return true;
			case OptionImpliedVolatility:
				quote.ImpliedVolatility = tick.Value is > 0 ? tick.Value : null;
				return true;
			default:
				return false;
		}
	}

	/// <summary>The quote has any price at all (bid, ask, last or close).</summary>
	public static bool HasPrice(QuoteDto quote) =>
		quote.Last != null || quote.Close != null || quote.Bid != null || quote.Ask != null;

	/// <summary>Last trade, or the previous close before the first trade of the day.</summary>
	public static double? GetDisplayPrice(QuoteDto quote) => quote.Last ?? quote.Close;

	public static double? GetChange(QuoteDto quote) =>
		quote.Last is { } last && quote.Close is { } close ? last - close : null;

	public static double? GetChangePercent(QuoteDto quote) =>
		quote.Last is { } last && quote.Close is { } close and not 0
			? (last - close) / close * 100
			: null;
}
