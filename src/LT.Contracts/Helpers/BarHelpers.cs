using LT.Contracts.Dtos.Charts;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

public static class BarHelpers
{
	/// <summary>
	/// Merges a 5-second real-time bar into chart bars. Intraday: a new bar starts on IB's grid
	/// (last bar time + n * bar length); daily and weekly: the last bar is updated in place. An
	/// intraday update that belongs to the bar before the last (live prices already started the
	/// next one, <see cref="MergePrice"/>) is folded into that bar. <paramref name="keepClose"/>
	/// keeps the bar's close where a newer live price put it, still adding the volume and
	/// extending high / low. Returns a new list; the input is not modified.
	/// </summary>
	public static IReadOnlyList<BarDto> Merge(
		IReadOnlyList<BarDto> bars,
		BarDto update,
		ChartTimeframe timeframe,
		bool keepClose = false
	)
	{
		if (bars.Count == 0)
			return [update];

		var last = bars[^1];
		var length = ChartTimeframeHelpers.GetBarLength(timeframe);
		var intraday = ChartTimeframeHelpers.IsIntraday(timeframe);
		if (update.Time < last.Time)
		{
			if (
				!intraday
				|| bars.Count < 2
				|| update.Time < bars[^2].Time
				|| update.Time >= bars[^2].Time + length
			)
				return bars;
			var folded = new List<BarDto>(bars);
			folded[^2] = Extend(bars[^2], update, keepClose);
			return folded;
		}

		var inLastBar = !intraday || update.Time < last.Time + length;

		var result = new List<BarDto>(bars.Count + 1);
		result.AddRange(bars.Take(bars.Count - 1));
		if (inLastBar)
		{
			result.Add(Extend(last, update, keepClose));
			return result;
		}

		result.Add(last);
		result.Add(
			new BarDto
			{
				Time = GetGridTime(last.Time, update.Time, length),
				Open = update.Open,
				High = update.High,
				Low = update.Low,
				Close = update.Close,
				Volume = update.Volume,
			}
		);
		return result;
	}

	/// <summary>
	/// Moves the last bar to a live price (a last trade, or the midpoint for forex): close =
	/// price, high / low extended. Intraday, a price past the last bar starts a new bar on IB's
	/// grid, open = high = low = close = price. Volume is never added — it comes with the
	/// 5-second bars (<see cref="Merge"/>), so nothing is counted twice; a new bar starts at 0.
	/// Prices older than the last bar are ignored. Returns a new list (the input when nothing
	/// changed); the input is not modified.
	/// </summary>
	public static IReadOnlyList<BarDto> MergePrice(
		IReadOnlyList<BarDto> bars,
		double price,
		DateTimeOffset time,
		ChartTimeframe timeframe
	)
	{
		if (bars.Count == 0)
			return bars;

		var last = bars[^1];
		if (time < last.Time)
			return bars;

		var length = ChartTimeframeHelpers.GetBarLength(timeframe);
		var inLastBar = !ChartTimeframeHelpers.IsIntraday(timeframe) || time < last.Time + length;
		if (inLastBar && last.Close == price && last.High >= price && last.Low <= price)
			return bars;

		var result = new List<BarDto>(bars.Count + 1);
		result.AddRange(bars.Take(bars.Count - 1));
		if (inLastBar)
		{
			result.Add(
				new BarDto
				{
					Time = last.Time,
					Open = last.Open,
					High = Math.Max(last.High, price),
					Low = Math.Min(last.Low, price),
					Close = price,
					Volume = last.Volume,
				}
			);
			return result;
		}

		result.Add(last);
		result.Add(
			new BarDto
			{
				Time = GetGridTime(last.Time, time, length),
				Open = price,
				High = price,
				Low = price,
				Close = price,
				Volume = 0,
			}
		);
		return result;
	}

	private static BarDto Extend(BarDto bar, BarDto update, bool keepClose) =>
		new()
		{
			Time = bar.Time,
			Open = bar.Open,
			High = Math.Max(bar.High, update.High),
			Low = Math.Min(bar.Low, update.Low),
			Close = keepClose ? bar.Close : update.Close,
			Volume = bar.Volume + update.Volume,
		};

	// Start of the bar containing time on the grid running from lastTime
	private static DateTimeOffset GetGridTime(
		DateTimeOffset lastTime,
		DateTimeOffset time,
		TimeSpan length
	) => lastTime + TimeSpan.FromTicks(length.Ticks * ((time - lastTime).Ticks / length.Ticks));

	/// <summary>
	/// The cache key of a bar request. The timeframe's own duration (Auto period) keeps the key
	/// it had before periods; another duration is part of the key.
	/// </summary>
	/// <summary>
	/// <paramref name="older"/> (a page of history before the chart's bars) put in front of
	/// <paramref name="bars"/>: only its bars older than the first one, sorted, without
	/// duplicate times. Returns <paramref name="bars"/> itself when nothing is older; the inputs
	/// are not modified.
	/// </summary>
	public static IReadOnlyList<BarDto> Prepend(
		IReadOnlyList<BarDto> older,
		IReadOnlyList<BarDto> bars
	)
	{
		var before = (bars.Count > 0 ? older.Where(x => x.Time < bars[0].Time) : older)
			.GroupBy(x => x.Time)
			.Select(x => x.Last())
			.OrderBy(x => x.Time)
			.ToList();
		if (before.Count == 0)
			return bars;
		before.AddRange(bars);
		return before;
	}

	/// <summary>
	/// How <paramref name="next"/> relates to <paramref name="previous"/>: the number of bars
	/// put in front of it (older history loaded; 0 when it only grew or changed at the end), or
	/// -1 when it is another series (its bars don't contain <paramref name="previous"/>'s first).
	/// Both are oldest first.
	/// </summary>
	public static int GetPrependedCount(IReadOnlyList<BarDto> previous, IReadOnlyList<BarDto> next)
	{
		if (previous.Count == 0 || next.Count == 0)
			return -1;
		var first = previous[0].Time;
		var low = 0;
		var high = next.Count - 1;
		while (low <= high)
		{
			var middle = low + (high - low) / 2;
			var time = next[middle].Time;
			if (time == first)
				return middle;
			if (time < first)
				low = middle + 1;
			else
				high = middle - 1;
		}
		return -1;
	}

	/// <summary>Cache key of a page of older bars: the series' key and where the page ends.</summary>
	public static string GetOlderCacheKey(
		int conId,
		ChartTimeframe timeframe,
		bool useRth,
		DateTimeOffset before
	) =>
		$"{GetCacheKey(conId, timeframe, useRth, ChartTimeframeHelpers.GetPageDuration(timeframe))}_to{before.ToUnixTimeSeconds()}";

	public static string GetCacheKey(
		int conId,
		ChartTimeframe timeframe,
		bool useRth,
		string? duration = null
	) =>
		$"{conId}_{timeframe}_{(useRth ? "rth" : "all")}"
		+ (
			duration == null || duration == ChartTimeframeHelpers.GetDuration(timeframe)
				? ""
				: $"_{duration.Replace(" ", "")}"
		);
}
