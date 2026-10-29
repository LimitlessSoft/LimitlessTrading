namespace LT.Contracts.Constants;

public static class OverviewConstants
{
	/// <summary>How often the Basic info tab's market state ("Open · closes 16:00") is re-read.</summary>
	public static readonly TimeSpan MarketStateRefreshInterval = TimeSpan.FromSeconds(30);
}
