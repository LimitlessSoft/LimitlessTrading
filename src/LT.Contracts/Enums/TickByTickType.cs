namespace LT.Contracts.Enums;

/// <summary>What an IB tick-by-tick subscription (<c>reqTickByTickData</c>) streams.</summary>
public enum TickByTickType
{
	/// <summary>Trades without odd lots.</summary>
	Last = 1,

	/// <summary>Trades including odd lots (flagged unreported).</summary>
	AllLast = 2,

	/// <summary>Best bid and ask.</summary>
	BidAsk = 3,

	/// <summary>Bid / ask midpoint (forex, which has no trades).</summary>
	MidPoint = 4,
}
