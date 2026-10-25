namespace LT.Contracts.Enums;

/// <summary>How long an order works: the day, until cancelled, or fill now and cancel the rest.</summary>
public enum TimeInForce
{
	Day = 0,
	Gtc = 1,
	Ioc = 2,
}
