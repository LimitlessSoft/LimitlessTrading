using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Calendar;

/// <summary>A Wall Street Horizon event type: its filter tag (<c>wshe_ed</c>) and name.</summary>
public class CalendarEventTypeDto
{
	public string Tag { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public CalendarEventKind Kind { get; set; }
}
