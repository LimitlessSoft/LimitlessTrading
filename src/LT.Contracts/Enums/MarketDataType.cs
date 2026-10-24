namespace LT.Contracts.Enums;

/// <summary>
/// IB reqMarketDataType values. Delayed still returns live data for instruments you have a
/// market data subscription for, so it is the safe default.
/// </summary>
public enum MarketDataType
{
	Live = 1,
	Frozen = 2,
	Delayed = 3,
	DelayedFrozen = 4,
}
