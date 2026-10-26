using System.Globalization;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Positions;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>Placing the user's own fills and position on a chart.</summary>
public static class ChartTradeHelpers
{
	/// <summary>
	/// Index of the bar that contains <paramref name="time"/>: the last bar starting at or before
	/// it, if the time is within that bar's length; -1 when no bar does (before the first bar, in
	/// a gap such as outside regular trading hours, or after the last bar). Daily and weekly bars
	/// carry a calendar date (midnight UTC), so the time is compared as its date in
	/// <paramref name="tradeDayZone"/>. <paramref name="bars"/> must be sorted by time.
	/// </summary>
	public static int FindBarIndex(
		IReadOnlyList<BarDto> bars,
		DateTimeOffset time,
		ChartTimeframe timeframe,
		TimeZoneInfo tradeDayZone
	)
	{
		if (bars.Count == 0)
			return -1;

		if (!ChartTimeframeHelpers.IsIntraday(timeframe))
		{
			var date = TimeZoneInfo.ConvertTime(time, tradeDayZone).Date;
			time = new DateTimeOffset(date, TimeSpan.Zero);
		}

		var low = 0;
		var high = bars.Count - 1;
		var found = -1;
		while (low <= high)
		{
			var middle = low + (high - low) / 2;
			if (bars[middle].Time <= time)
			{
				found = middle;
				low = middle + 1;
			}
			else
				high = middle - 1;
		}

		if (found < 0)
			return -1;
		return time < bars[found].Time + ChartTimeframeHelpers.GetBarLength(timeframe) ? found : -1;
	}

	/// <summary>
	/// The price at which the position breaks even: IB's average cost per unit (commissions
	/// included). IB reports it per contract for options and futures, so it is divided by the
	/// contract multiplier. Null for a flat position.
	/// </summary>
	public static double? GetBreakEvenPrice(PositionDto position)
	{
		if (position.Quantity == 0 || position.AverageCost <= 0)
			return null;
		var multiplier =
			double.TryParse(
				position.Contract.Multiplier,
				NumberStyles.Float,
				CultureInfo.InvariantCulture,
				out var value
			)
			&& value > 0
				? value
				: 1;
		return position.AverageCost / multiplier;
	}
}
