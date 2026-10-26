namespace LT.Contracts.Enums;

/// <summary>What a cache entry holds; also its folder under the cache directory. Append only.</summary>
public enum CacheType
{
	ChartBars,
	Contracts,
	OptionChains,
	Executions,
	Snapshots,
}
