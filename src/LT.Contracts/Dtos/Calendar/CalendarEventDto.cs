using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Calendar;

/// <summary>
/// One event of the calendar: a corporate event (an earnings date, a dividend, a meeting …) or an
/// economic release (no symbol; <see cref="Company"/> is the release, <see cref="TypeName"/> its
/// country).
/// </summary>
public class CalendarEventDto
{
	public DateOnly Date { get; set; }
	public CalendarEventKind Kind { get; set; }

	/// <summary>WSH's type tag (<c>wshe_ed</c>); empty when the event named none.</summary>
	public string TypeTag { get; set; } = string.Empty;

	/// <summary>The type's name from WSH's metadata, else made from the tag.</summary>
	public string TypeName { get; set; } = string.Empty;

	public string Symbol { get; set; } = string.Empty;
	public string Company { get; set; } = string.Empty;

	/// <summary>IB's contract id when WSH gives one; 0 when not.</summary>
	public int ConId { get; set; }

	public CalendarEventTime Time { get; set; }

	/// <summary>
	/// The source's own time text when it is not one of <see cref="CalendarEventTime"/> (an
	/// economic release's "08:30", "All day").
	/// </summary>
	public string TimeText { get; set; } = string.Empty;

	/// <summary>Short extra facts: "EPS est. $3.43", "2.1077 · pays 4 Nov", "3:1", "Act. 0.07 · Cons. -0.10 · Prev. -0.08".</summary>
	public string Detail { get; set; } = string.Empty;

	/// <summary>The source that reported it (the first one, when several did).</summary>
	public CalendarSource Source { get; set; }
}
