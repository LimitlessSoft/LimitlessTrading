namespace LT.Contracts.Enums;

/// <summary>One of nasdaq.com's calendars (one endpoint each). Not persisted.</summary>
public enum NasdaqCalendar
{
	Earnings = 0,
	Dividends = 1,
	Splits = 2,
	Economic = 3,
}
