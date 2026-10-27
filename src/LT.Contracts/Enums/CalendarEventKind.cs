namespace LT.Contracts.Enums;

/// <summary>
/// What a calendar event is: a corporate event (from WSH's type tag and name, or Nasdaq's calendar
/// it came from) or an economic release. Integer-backed; add values at the end.
/// </summary>
public enum CalendarEventKind
{
	Earnings = 0,
	Dividend = 1,
	Split = 2,
	Conference = 3,
	ShareholderMeeting = 4,
	BoardMeeting = 5,
	Other = 6,

	/// <summary>A macroeconomic release or event (GDP, CPI, a central bank speech, a holiday): no symbol.</summary>
	Economic = 7,
}
