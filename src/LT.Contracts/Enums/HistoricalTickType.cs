namespace LT.Contracts.Enums;

/// <summary>Which past ticks <c>IIbkrApiClient.GetHistoricalTicksAsync</c> asks IB for (reqHistoricalTicks).</summary>
public enum HistoricalTickType
{
	/// <summary>Every trade, odd lots included (as <see cref="TickByTickType.AllLast"/> ticks).</summary>
	Trades = 1,

	/// <summary>Best bid and ask.</summary>
	BidAsk = 2,

	/// <summary>Bid / ask midpoint.</summary>
	MidPoint = 3,
}
