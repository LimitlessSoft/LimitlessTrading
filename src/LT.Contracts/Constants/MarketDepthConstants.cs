namespace LT.Contracts.Constants;

public static class MarketDepthConstants
{
	/// <summary>
	/// IB allows 3 market depth subscriptions at a time per account by default (error 309 above
	/// it, seen 2026-10-26); TWS and other API clients count against the same limit.
	/// </summary>
	public const int MaxSubscriptions = 3;

	/// <summary>Rows per side; 20 and 50 were served on SMART depth (AAPL, 2026-10-26).</summary>
	public const int DefaultRows = 20;
	public const int MaxRows = 50;

	/// <summary>Depth moves fast; the widget redraws at most this often.</summary>
	public static readonly TimeSpan UiFlushInterval = TimeSpan.FromMilliseconds(100);

	/// <summary>"Max number (3) of market depth requests has been reached".</summary>
	public const int MaxSubscriptionsErrorCode = 309;

	/// <summary>
	/// No depth subscription for the exchange: 354 "not subscribed" (ARCA, IBIS), 10089
	/// "requires additional subscription for API" (NASDAQ TotalView).
	/// </summary>
	public static readonly IReadOnlyList<int> NotSubscribedErrorCodes = [354, 10089];
}
