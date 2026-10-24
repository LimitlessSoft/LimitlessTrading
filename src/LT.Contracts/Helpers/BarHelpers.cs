using LT.Contracts.Dtos.Charts;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

public static class BarHelpers
{
	/// <summary>
	/// Merges a 5-second real-time bar into chart bars. Intraday: a new bar starts on IB's grid
	/// (last bar time + n * bar length); daily and weekly: the last bar is updated in place.
	/// Returns a new list; the input is not modified.
	/// </summary>
	public static IReadOnlyList<BarDto> Merge(
		IReadOnlyList<BarDto> bars,
		BarDto update,
		ChartTimeframe timeframe
	)
	{
		if (bars.Count == 0)
			return [update];

		var last = bars[^1];
		if (update.Time < last.Time)
			return bars;

		var length = ChartTimeframeHelpers.GetBarLength(timeframe);
		var inLastBar =
			!ChartTimeframeHelpers.IsIntraday(timeframe) || update.Time < last.Time + length;

		var result = new List<BarDto>(bars.Count + 1);
		result.AddRange(bars.Take(bars.Count - 1));
		if (inLastBar)
		{
			result.Add(
				new BarDto
				{
					Time = last.Time,
					Open = last.Open,
					High = Math.Max(last.High, update.High),
					Low = Math.Min(last.Low, update.Low),
					Close = update.Close,
					Volume = last.Volume + update.Volume,
				}
			);
			return result;
		}

		var periods = (long)((update.Time - last.Time).Ticks / length.Ticks);
		result.Add(last);
		result.Add(
			new BarDto
			{
				Time = last.Time + TimeSpan.FromTicks(length.Ticks * periods),
				Open = update.Open,
				High = update.High,
				Low = update.Low,
				Close = update.Close,
				Volume = update.Volume,
			}
		);
		return result;
	}

	public static string GetCacheKey(int conId, ChartTimeframe timeframe, bool useRth) =>
		$"{conId}_{timeframe}_{(useRth ? "rth" : "all")}";
}
