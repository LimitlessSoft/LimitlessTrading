namespace LT.Contracts.Enums;

/// <summary>Where a day of account history came from. Persisted: add values at the end.</summary>
public enum AccountDaySource
{
	/// <summary>Recorded by the app from the socket API while connected.</summary>
	Recorded,

	/// <summary>Imported from an Activity Flex statement (end-of-day NAV, cash flows).</summary>
	Flex,
}
