namespace LT.Contracts.Enums;

/// <summary>Which side started a trade (its aggressor), as a footprint counts it.</summary>
public enum FootprintSide
{
	/// <summary>Can't be told (the first trade with nothing before it, a trade at the midpoint).</summary>
	Unknown = 0,

	/// <summary>A seller hit the bid.</summary>
	Bid = 1,

	/// <summary>A buyer lifted the ask.</summary>
	Ask = 2,
}
