namespace LT.Contracts.Constants;

/// <summary>
/// Owner labels callers pass with a market data line, shown in the status bar's stream list so
/// the user can see what uses the <see cref="MarketDataConstants.MaxLines"/> lines.
/// </summary>
public static class MarketDataStreamOwners
{
	public const string Watchlist = "Watchlist";
	public const string Chart = "Chart";
	public const string OptionChain = "Option chain";
	public const string OptionChainUnderlying = "Option chain (underlying price)";
	public const string OrderTicket = "Order ticket";
	public const string PriceAlerts = "Price alerts";
	public const string News = "News";
	public const string Overview = "Overview";
}
