namespace LT.Contracts.Enums;

/// <summary>Which events the Calendar widget shows (saved per widget; integer-backed, add at the end).</summary>
public enum CalendarEventFilter
{
	All = 0,

	/// <summary>Earnings dates only.</summary>
	Earnings = 1,

	/// <summary>Corporate events other than earnings: dividends, splits, conferences, meetings …</summary>
	Other = 2,

	/// <summary>Economic releases only (GDP, CPI, payrolls, central bank speeches …).</summary>
	Economic = 3,
}
