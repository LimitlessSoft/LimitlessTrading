namespace LT.Contracts.Dtos.Calendar;

/// <summary>One day of the calendar and its events, in display order.</summary>
public class CalendarDayDto
{
	public DateOnly Date { get; set; }
	public IReadOnlyList<CalendarEventDto> Events { get; set; } = [];

	/// <summary>WSH returned as many events as one request may: there may be more that day.</summary>
	public bool IsTruncated { get; set; }
}
