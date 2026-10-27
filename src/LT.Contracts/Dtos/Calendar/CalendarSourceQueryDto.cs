using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Calendar;

/// <summary>What the calendar manager asks one source for: these days, events passing the filter.</summary>
public class CalendarSourceQueryDto
{
	/// <summary>Consecutive days, earliest first, at most <c>CalendarConstants.MaxDaysPerLoad</c>.</summary>
	public IReadOnlyList<DateOnly> Days { get; set; } = [];

	public CalendarEventFilter Filter { get; set; }

	/// <summary>Ask again instead of answering from the events loaded recently.</summary>
	public bool Refresh { get; set; }
}
