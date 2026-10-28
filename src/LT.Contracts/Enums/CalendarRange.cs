namespace LT.Contracts.Enums;

/// <summary>Which days the Calendar widget shows (saved per widget; integer-backed).</summary>
public enum CalendarRange
{
	/// <summary>Monday to Sunday of the current week.</summary>
	ThisWeek = 0,

	/// <summary>Monday to Sunday of the next week.</summary>
	NextWeek = 1,

	/// <summary>The 1st to the last day of the current month.</summary>
	ThisMonth = 2,

	/// <summary>Today only.</summary>
	Today = 3,
}
