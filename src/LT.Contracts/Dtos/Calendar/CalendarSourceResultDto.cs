using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Calendar;

/// <summary>
/// One source's answer: its events of the days asked for, or, when <see cref="Status"/> is not
/// <see cref="CalendarStatus.Loaded"/>, none and a <see cref="Message"/> ready to show.
/// </summary>
public class CalendarSourceResultDto
{
	public CalendarStatus Status { get; set; }
	public string? Message { get; set; }
	public IReadOnlyList<CalendarEventDto> Events { get; set; } = [];

	/// <summary>Days where the source returned as many events as one request may.</summary>
	public IReadOnlySet<DateOnly> TruncatedDays { get; set; } = new HashSet<DateOnly>();

	public static CalendarSourceResultDto Failure(CalendarStatus status, string message) =>
		new() { Status = status, Message = message };
}
