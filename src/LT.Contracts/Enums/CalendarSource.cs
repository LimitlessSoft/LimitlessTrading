namespace LT.Contracts.Enums;

/// <summary>Where calendar events come from (integer-backed; add values at the end).</summary>
public enum CalendarSource
{
	/// <summary>IB's Wall Street Horizon corporate events: needs the WSH subscription.</summary>
	WallStreetHorizon = 0,

	/// <summary>nasdaq.com's public calendars: earnings, dividends, splits, economic releases.</summary>
	Nasdaq = 1,
}
