using LT.Contracts.Dtos.MarketDepth;
using LT.Contracts.Requests.MarketDepth;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>
/// Order books over IB market depth. IB allows only a few depth subscriptions at a time
/// (<c>MarketDepthConstants.MaxSubscriptions</c>), so every one goes through here.
/// </summary>
public interface IMarketDepthManager
{
	/// <summary>Depth subscriptions currently open by this app.</summary>
	int ActiveSubscriptions { get; }

	/// <summary>
	/// The contract's order book after every change, until the enumeration ends. Counts against
	/// the depth subscription limit meanwhile; throws at the limit, and with a readable message
	/// when IB refuses (no depth subscription for the exchange, IB's own limit reached).
	/// </summary>
	IAsyncEnumerable<OrderBookDto> SubscribeOrderBookAsync(
		SubscribeOrderBookRequest request,
		CancellationToken cancellationToken = default
	);
}
