namespace LT.Contracts.Enums;

/// <summary>When in the trading day an event (an earnings release) happens, if WSH says.</summary>
public enum CalendarEventTime
{
	Unknown = 0,
	BeforeOpen = 1,
	DuringMarket = 2,
	AfterClose = 3,
}
