namespace LT.Contracts.Enums;

/// <summary>What a line of IBC / Gateway console output tells us (best effort).</summary>
public enum IbcOutputEvent
{
	None = 0,
	SecondFactorRequested = 1,
	LoginCompleted = 2,
	LoginFailed = 3,
}
