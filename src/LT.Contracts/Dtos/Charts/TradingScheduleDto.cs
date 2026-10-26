namespace LT.Contracts.Dtos.Charts;

/// <summary>
/// A contract's regular (liquid) sessions, from IB's contract details: the days IB lists, and a
/// usual week for every other day (history). Built and read by TradingHoursHelpers.
/// </summary>
public class TradingScheduleDto
{
	/// <summary>The exchange's zone; sessions are wall-clock times in it.</summary>
	public TimeZoneInfo Zone { get; set; } = TimeZoneInfo.Utc;

	/// <summary>The days IB listed, by the day a session starts; an empty list is a closed day.</summary>
	public IReadOnlyDictionary<DateOnly, IReadOnlyList<TradingSessionDto>> Days { get; set; } =
		new Dictionary<DateOnly, IReadOnlyList<TradingSessionDto>>();

	/// <summary>Sessions of a day IB didn't list; a missing weekday has none (weekend).</summary>
	public IReadOnlyDictionary<DayOfWeek, IReadOnlyList<TradingSessionDto>> Week { get; set; } =
		new Dictionary<DayOfWeek, IReadOnlyList<TradingSessionDto>>();
}
