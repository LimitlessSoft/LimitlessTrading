namespace LT.Contracts.Enums;

/// <summary>What a depth update does to its row (IB's values).</summary>
public enum MarketDepthOperation
{
	Insert = 0,
	Update = 1,
	Delete = 2,
}
