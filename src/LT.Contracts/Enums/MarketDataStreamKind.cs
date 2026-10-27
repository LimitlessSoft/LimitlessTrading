namespace LT.Contracts.Enums;

/// <summary>What a market data line streams (see <c>IMarketDataManager.GetActiveStreams</c>).</summary>
public enum MarketDataStreamKind
{
	/// <summary>Level 1 quote (reqMktData), possibly with generic ticks on the same line.</summary>
	Quote,

	/// <summary>Live news headlines of a contract or a broad tape (reqMktData mdoff,292).</summary>
	News,
}
