using LT.Contracts.Enums;

namespace LT.Contracts.Requests.Calendar;

/// <summary>
/// Market-wide corporate events of a date range. <see cref="Refresh"/> asks IB again instead of
/// answering from the events loaded recently.
/// </summary>
public class GetCalendarEventsRequest
{
	public CalendarRange Range { get; set; }
	public CalendarEventFilter Filter { get; set; }
	public bool Refresh { get; set; }
}
