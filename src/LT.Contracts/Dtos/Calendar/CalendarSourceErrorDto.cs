using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Calendar;

/// <summary>A calendar source that gave no events this load, and why ("no WSH subscription …").</summary>
public class CalendarSourceErrorDto
{
	public CalendarSource Source { get; set; }
	public CalendarStatus Status { get; set; }
	public string Message { get; set; } = string.Empty;
}
