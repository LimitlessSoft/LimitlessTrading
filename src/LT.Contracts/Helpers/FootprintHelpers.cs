using System.Globalization;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>
/// Footprint bars: which side started each trade, the trades of a bar by price, its rows as
/// drawn (point of control, diagonal imbalances) and their numbers.
/// </summary>
public static class FootprintHelpers
{
	/// <summary>
	/// The contract's minimum tick, or <see cref="FootprintConstants.FallbackTickSize"/> when IB
	/// gives none (0, or its unset value <see cref="double.MaxValue"/>).
	/// </summary>
	public static double GetTickSize(double minTick) =>
		minTick > 0 && minTick < double.MaxValue && double.IsFinite(minTick)
			? minTick
			: FootprintConstants.FallbackTickSize;

	/// <summary>The price in ticks, rounded to the nearest (sub-tick prints land on a tick).</summary>
	public static long GetTickIndex(double price, double tickSize) =>
		(long)Math.Round(price / tickSize, MidpointRounding.AwayFromZero);

	/// <summary>
	/// The quote rule: a trade at or above the ask was a buyer's, at or below the bid a seller's,
	/// inside the spread the side it is nearer; at the midpoint, or without a usable quote,
	/// <see cref="FootprintSide.Unknown"/>.
	/// </summary>
	public static FootprintSide ClassifyByQuote(double price, double bid, double ask)
	{
		if (bid <= 0 || ask <= 0 || ask < bid)
			return FootprintSide.Unknown;
		if (price >= ask)
			return FootprintSide.Ask;
		if (price <= bid)
			return FootprintSide.Bid;
		// Nearer the ask or the bid; equally near (to rounding) is the midpoint
		var nearerAsk = (price - bid) - (ask - price);
		var tolerance = 1e-9 * Math.Max(1, Math.Abs(price));
		return nearerAsk > tolerance ? FootprintSide.Ask
			: nearerAsk < -tolerance ? FootprintSide.Bid
			: FootprintSide.Unknown;
	}

	/// <summary>
	/// The tick rule: a trade above the one before it was a buyer's (uptick), below it a seller's;
	/// at the same price it keeps the side of the one before. Unknown with nothing before it.
	/// </summary>
	public static FootprintSide ClassifyByTickRule(
		double price,
		double? previousPrice,
		FootprintSide previousSide
	) =>
		previousPrice is not { } previous ? FootprintSide.Unknown
		: price > previous ? FootprintSide.Ask
		: price < previous ? FootprintSide.Bid
		: previousSide;

	/// <summary>
	/// Past trades (oldest first, no quotes known) by the tick rule, from the trade before them
	/// (<paramref name="previous"/>, null when there is none).
	/// </summary>
	public static List<FootprintTradeDto> ClassifyByTickRule(
		IEnumerable<TickByTickDto> trades,
		TickByTickDto? previous
	)
	{
		var result = new List<FootprintTradeDto>();
		double? previousPrice = previous?.Price;
		var side = FootprintSide.Unknown;
		foreach (var trade in trades)
		{
			side = ClassifyByTickRule(trade.Price, previousPrice, side);
			previousPrice = trade.Price;
			result.Add(
				new FootprintTradeDto
				{
					Time = trade.Time,
					Price = trade.Price,
					Size = trade.Size,
					Side = side,
					ByTickRule = true,
				}
			);
		}
		return result;
	}

	/// <summary>
	/// A live trade: by the quote rule against <paramref name="quote"/> (the latest BidAsk tick),
	/// by the tick rule against the trade before it when there is no quote or the trade is at its
	/// midpoint.
	/// </summary>
	public static FootprintTradeDto Classify(
		TickByTickDto trade,
		TickByTickDto? quote,
		double? previousPrice,
		FootprintSide previousSide
	)
	{
		var side =
			quote != null
				? ClassifyByQuote(trade.Price, quote.BidPrice, quote.AskPrice)
				: FootprintSide.Unknown;
		var byTickRule = side == FootprintSide.Unknown;
		if (byTickRule)
			side = ClassifyByTickRule(trade.Price, previousPrice, previousSide);
		return new FootprintTradeDto
		{
			Time = trade.Time,
			Price = trade.Price,
			Size = trade.Size,
			Side = side,
			ByTickRule = byTickRule,
		};
	}

	/// <summary>
	/// Counts a trade at its price (rounded to the bar's tick); a trade of unknown side counts half
	/// on each side, so the bar's volume stays whole.
	/// </summary>
	public static void AddTrade(FootprintBarDto bar, FootprintTradeDto trade) =>
		AddTrade(bar, trade.Price, trade.Size, trade.Side);

	/// <inheritdoc cref="AddTrade(FootprintBarDto, FootprintTradeDto)"/>
	public static void AddTrade(FootprintBarDto bar, double price, decimal size, FootprintSide side)
	{
		if (size <= 0 || price <= 0)
			return;
		var index = GetTickIndex(price, bar.TickSize);
		if (!bar.Levels.TryGetValue(index, out var level))
			bar.Levels[index] = level = new FootprintLevelDto { TickIndex = index };
		var (bid, ask) = side switch
		{
			FootprintSide.Bid => (size, 0m),
			FootprintSide.Ask => (0m, size),
			_ => (size / 2, size / 2),
		};
		level.BidVolume += bid;
		level.AskVolume += ask;
		bar.BidVolume += bid;
		bar.AskVolume += ask;
	}

	/// <summary>
	/// A trade counted with an unknown side (half on each, <see cref="AddTrade"/>) once its side
	/// is known: its volume moves to that side. Nothing for <see cref="FootprintSide.Unknown"/>.
	/// </summary>
	public static void SetSide(
		FootprintBarDto bar,
		long tickIndex,
		decimal size,
		FootprintSide side
	)
	{
		if (side == FootprintSide.Unknown || size <= 0)
			return;
		if (!bar.Levels.TryGetValue(tickIndex, out var level))
			return;
		var half = size / 2;
		var (bid, ask) = side == FootprintSide.Bid ? (half, -half) : (-half, half);
		level.BidVolume += bid;
		level.AskVolume += ask;
		bar.BidVolume += bid;
		bar.AskVolume += ask;
	}

	/// <summary>
	/// The tick rule's side for the first trade of a run at <paramref name="price"/> given the
	/// trade before it (null: none known).
	/// </summary>
	public static FootprintSide GetSideAfter(double price, FootprintTradeDto? previous) =>
		previous == null
			? FootprintSide.Unknown
			: ClassifyByTickRule(price, previous.Price, previous.Side);

	/// <summary>
	/// The start of the chart bar a trade at <paramref name="time"/> belongs to: the bar containing
	/// it, or past the last bar the bar on its grid that is still to come (live trades run ahead
	/// of the bars); null in a gap or before the first bar (intraday only).
	/// </summary>
	public static DateTimeOffset? GetBarTime(
		IReadOnlyList<BarDto> bars,
		DateTimeOffset time,
		ChartTimeframe timeframe
	)
	{
		var index = ChartTradeHelpers.FindBarIndex(bars, time, timeframe, TimeZoneInfo.Utc);
		if (index >= 0)
			return bars[index].Time;
		if (bars.Count == 0 || time < bars[^1].Time)
			return null;
		return BarHelpers.GetGridTime(
			bars[^1].Time,
			time,
			ChartTimeframeHelpers.GetBarLength(timeframe)
		);
	}

	/// <summary>
	/// Where a chart's footprint backfill stops: the first bar of the latest bar's day in
	/// <paramref name="zone"/> (the current session); null without bars.
	/// </summary>
	public static DateTimeOffset? GetSessionStart(IReadOnlyList<BarDto> bars, TimeZoneInfo zone)
	{
		if (bars.Count == 0)
			return null;
		var day = TimeZoneInfo.ConvertTime(bars[^1].Time, zone).Date;
		var first = bars.Count - 1;
		while (first > 0 && TimeZoneInfo.ConvertTime(bars[first - 1].Time, zone).Date == day)
			first--;
		return bars[first].Time;
	}

	/// <summary>
	/// Ticks per drawn row so a row is at least <paramref name="minRowHeight"/> high: 1, 2, 5, 10,
	/// 20, 50 … (zooming out merges rows instead of shrinking the text).
	/// </summary>
	public static int GetTicksPerRow(double pixelsPerTick, double minRowHeight)
	{
		if (pixelsPerTick <= 0 || double.IsNaN(pixelsPerTick))
			return 1;
		var needed = minRowHeight / pixelsPerTick;
		for (var magnitude = 1L; magnitude < int.MaxValue / 10; magnitude *= 10)
			foreach (var step in new[] { 1, 2, 5 })
				if (step * magnitude >= needed)
					return (int)(step * magnitude);
		return int.MaxValue / 10;
	}

	/// <summary>
	/// The bar's rows as drawn, lowest first: its levels grouped by
	/// <paramref name="ticksPerRow"/>, every row from its lowest to its highest traded price
	/// (empty ones too). The point of control is the row with the most volume (the lowest of
	/// equal ones). Imbalances are diagonal — the ask of a row against the bid one row down, the
	/// bid against the ask one row up — at <paramref name="imbalanceRatio"/> (3 = 300 %); an
	/// empty opposite row inside the bar counts as an imbalance, there is none past its ends.
	/// With <paramref name="barLow"/> / <paramref name="barHigh"/> (the chart bar's), prices outside
	/// them are left out: IB's trades include prints its bars don't count (late and
	/// out-of-sequence reports far from the market), which would stretch the bar into a column of
	/// empty rows.
	/// </summary>
	public static IReadOnlyList<FootprintRowDto> GetRows(
		FootprintBarDto bar,
		int ticksPerRow,
		double imbalanceRatio,
		double? barLow = null,
		double? barHigh = null
	)
	{
		if (bar.Levels.Count == 0)
			return [];
		ticksPerRow = Math.Max(1, ticksPerRow);
		var lowIndex = barLow is { } l ? GetTickIndex(l, bar.TickSize) : long.MinValue;
		var highIndex = barHigh is { } h ? GetTickIndex(h, bar.TickSize) : long.MaxValue;
		var grouped = new Dictionary<long, (decimal Bid, decimal Ask)>();
		foreach (var level in bar.Levels.Values)
		{
			if (level.BidVolume == 0 && level.AskVolume == 0)
				continue;
			if (level.TickIndex < lowIndex || level.TickIndex > highIndex)
				continue;
			var key = FloorDiv(level.TickIndex, ticksPerRow);
			var (bid, ask) = grouped.GetValueOrDefault(key);
			grouped[key] = (bid + level.BidVolume, ask + level.AskVolume);
		}
		if (grouped.Count == 0)
			return [];
		var low = grouped.Keys.Min();
		var high = grouped.Keys.Max();
		var rows = new List<FootprintRowDto>((int)(high - low + 1));
		for (var key = low; key <= high; key++)
		{
			var (bid, ask) = grouped.GetValueOrDefault(key);
			rows.Add(
				new FootprintRowDto
				{
					Price = key * ticksPerRow * bar.TickSize,
					HighPrice = (key * ticksPerRow + ticksPerRow - 1) * bar.TickSize,
					BidVolume = bid,
					AskVolume = ask,
				}
			);
		}

		var ratio = (decimal)Math.Max(1, imbalanceRatio);
		var poc = 0;
		for (var i = 0; i < rows.Count; i++)
		{
			var row = rows[i];
			if (row.Volume > rows[poc].Volume)
				poc = i;
			if (i > 0 && row.AskVolume > 0)
				row.IsAskImbalance = row.AskVolume >= ratio * rows[i - 1].BidVolume;
			if (i < rows.Count - 1 && row.BidVolume > 0)
				row.IsBidImbalance = row.BidVolume >= ratio * rows[i + 1].AskVolume;
		}
		rows[poc].IsPoc = true;
		return rows;
	}

	/// <summary>
	/// Several bars' levels summed into one (a volume profile over them); the bars share a tick
	/// size.
	/// </summary>
	public static FootprintBarDto Combine(IEnumerable<FootprintBarDto> bars, double tickSize)
	{
		var result = new FootprintBarDto { TickSize = tickSize };
		foreach (var bar in bars)
		{
			foreach (var level in bar.Levels.Values)
			{
				if (!result.Levels.TryGetValue(level.TickIndex, out var sum))
					result.Levels[level.TickIndex] = sum = new FootprintLevelDto
					{
						TickIndex = level.TickIndex,
					};
				sum.BidVolume += level.BidVolume;
				sum.AskVolume += level.AskVolume;
			}
			result.BidVolume += bar.BidVolume;
			result.AskVolume += bar.AskVolume;
		}
		return result;
	}

	/// <summary>
	/// A footprint cell's volume, short enough for a narrow cell: 0, 0.25 (fractional crypto
	/// sizes), 1234, 12.3K, 1.2M.
	/// </summary>
	public static string FormatVolume(decimal volume)
	{
		var culture = CultureInfo.InvariantCulture;
		var value = Math.Abs(volume);
		var sign = volume < 0 ? "-" : "";
		if (value >= 1_000_000)
			return sign
				+ (value / 1_000_000).ToString(value >= 10_000_000 ? "0" : "0.#", culture)
				+ "M";
		if (value >= 10_000)
			return sign + (value / 1_000).ToString(value >= 100_000 ? "0" : "0.#", culture) + "K";
		return sign + value.ToString(value >= 100 ? "0" : "0.##", culture);
	}

	private static long FloorDiv(long value, int divisor) =>
		value >= 0 ? value / divisor : -((-value + divisor - 1) / divisor);
}
