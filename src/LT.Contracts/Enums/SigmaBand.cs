namespace LT.Contracts.Enums;

/// <summary>
/// How far a strike is from the underlying price in standard deviations of the expected move to
/// expiration; <see cref="None"/> when that is unknown.
/// </summary>
public enum SigmaBand
{
	None = 0,
	WithinOne = 1,
	WithinTwo = 2,
	WithinThree = 3,
	BeyondThree = 4,
}
