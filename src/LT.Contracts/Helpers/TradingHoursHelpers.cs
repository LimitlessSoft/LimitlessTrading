using System.Globalization;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Charts;

namespace LT.Contracts.Helpers;

/// <summary>
/// A contract's regular sessions from IB's contract details (liquidHours + timeZoneId), and
/// whether a chart bar falls in one — bars outside are pre- / post-market.
/// </summary>
public static class TradingHoursHelpers
{
	/// <summary>
	/// Reads IB's hours, "20261026:0930-20261026:1600;20261027:CLOSED" (or the older
	/// "20261026:0930-1600,1700-1800"). IB lists only a few days around today, so every other
	/// day takes the usual sessions: the most common of the listed open days, on Monday to Friday
	/// and on a weekend day that was listed open (a market trading 7 days). A listed weekday that
	/// is closed (a holiday) does not make that weekday closed in history. Null when nothing
	/// usable is there or the zone is unknown: the chart then draws every bar as regular.
	/// </summary>
	public static TradingScheduleDto? Parse(string? hours, string? timeZoneId)
	{
		if (string.IsNullOrWhiteSpace(hours) || FindZone(timeZoneId) is not { } zone)
			return null;

		var days = new Dictionary<DateOnly, List<TradingSessionDto>>();
		foreach (
			var entry in hours.Split(
				';',
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
			)
		)
		{
			var colon = entry.IndexOf(':');
			if (colon < 0 || !TryParseDate(entry[..colon], out var date))
				continue;
			if (!days.TryGetValue(date, out var sessions))
				days[date] = sessions = [];
			var rest = entry[(colon + 1)..];
			if (rest.Equals("CLOSED", StringComparison.OrdinalIgnoreCase))
				continue;
			foreach (var range in rest.Split(',', StringSplitOptions.TrimEntries))
			{
				var dash = range.IndexOf('-');
				if (
					dash < 0
					|| !TryParsePoint(range[..dash], date, out var start)
					|| !TryParsePoint(range[(dash + 1)..], date, out var end)
				)
					continue;
				// Older format: a session past midnight ends "earlier" on the same date
				if (end <= start)
					end = end.AddDays(1);
				sessions.Add(
					new TradingSessionDto
					{
						Start = TimeOnly.FromDateTime(start),
						Length = end - start,
					}
				);
			}
		}

		var open = days.Where(x => x.Value.Count > 0).ToList();
		if (open.Count == 0)
			return null;
		var usual = open.GroupBy(x => GetPatternKey(x.Value))
			.OrderByDescending(x => x.Count())
			.ThenByDescending(x => x.Max(y => y.Key))
			.First()
			.First()
			.Value;
		var week = new Dictionary<DayOfWeek, IReadOnlyList<TradingSessionDto>>();
		foreach (var day in Enum.GetValues<DayOfWeek>())
			if (
				day is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
				|| open.Any(x => x.Key.DayOfWeek == day)
			)
				week[day] = usual;

		return new TradingScheduleDto
		{
			Zone = zone,
			Days = days.ToDictionary(x => x.Key, x => (IReadOnlyList<TradingSessionDto>)x.Value),
			Week = week,
		};
	}

	/// <summary>
	/// True when the bar [start, start + length) overlaps a regular session: a bar holding the
	/// open or the close counts as regular, only a bar wholly outside is pre- / post-market.
	/// </summary>
	public static bool IsRegular(TradingScheduleDto schedule, DateTimeOffset start, TimeSpan length)
	{
		var end = start + length;
		var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(start, schedule.Zone).DateTime);
		// A session listed under the previous day may run past midnight
		for (var day = date.AddDays(-1); day <= date.AddDays(1); day = day.AddDays(1))
			foreach (var session in GetSessions(schedule, day))
			{
				var wall = day.ToDateTime(session.Start);
				var from = new DateTimeOffset(wall, schedule.Zone.GetUtcOffset(wall));
				if (start < from + session.Length && end > from)
					return true;
			}
		return false;
	}

	/// <summary>The regular sessions of an exchange day: as IB listed it, else the usual week's.</summary>
	public static IReadOnlyList<TradingSessionDto> GetSessions(
		TradingScheduleDto schedule,
		DateOnly date
	) =>
		schedule.Days.TryGetValue(date, out var listed) ? listed
		: schedule.Week.TryGetValue(date.DayOfWeek, out var usual) ? usual
		: [];

	/// <summary>
	/// An IB zone id ("US/Eastern", "MET", "Japan"…): as named, else the IANA zone it is an old
	/// name of (<see cref="IbTimeZoneConstants.Aliases"/>); null when neither is known here.
	/// </summary>
	public static TimeZoneInfo? FindZone(string? timeZoneId)
	{
		if (string.IsNullOrWhiteSpace(timeZoneId))
			return null;
		var id = timeZoneId.Trim();
		return TryFindZone(id)
			?? (
				IbTimeZoneConstants.Aliases.TryGetValue(id, out var alias)
					? TryFindZone(alias)
					: null
			);
	}

	private static TimeZoneInfo? TryFindZone(string id)
	{
		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById(id);
		}
		catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
		{
			return null;
		}
	}

	private static string GetPatternKey(IEnumerable<TradingSessionDto> sessions) =>
		string.Join(",", sessions.Select(x => $"{x.Start:HHmm}+{x.Length.TotalMinutes}"));

	private static bool TryParseDate(string text, out DateOnly date) =>
		DateOnly.TryParseExact(
			text.Trim(),
			"yyyyMMdd",
			CultureInfo.InvariantCulture,
			DateTimeStyles.None,
			out date
		);

	// "20261026:1600", or "1600" on the entry's own date
	private static bool TryParsePoint(string text, DateOnly entryDate, out DateTime point)
	{
		point = default;
		var colon = text.IndexOf(':');
		var date = entryDate;
		if (colon >= 0 && !TryParseDate(text[..colon], out date))
			return false;
		var clock = (colon >= 0 ? text[(colon + 1)..] : text).Trim();
		// The end of a whole day may be written 2400
		if (clock == "2400")
		{
			point = date.AddDays(1).ToDateTime(TimeOnly.MinValue);
			return true;
		}
		if (
			!TimeOnly.TryParseExact(
				clock,
				"HHmm",
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out var time
			)
		)
			return false;
		point = date.ToDateTime(time);
		return true;
	}
}
