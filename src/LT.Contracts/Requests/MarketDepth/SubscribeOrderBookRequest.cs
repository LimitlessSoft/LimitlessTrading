using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;

namespace LT.Contracts.Requests.MarketDepth;

/// <summary>
/// The order book of a contract. With <see cref="PreferSmartDepth"/> a contract routed through
/// SMART gets SMART depth (every exchange the account has depth for); otherwise, and for
/// contracts on one exchange (crypto, forex), the depth of its listing exchange.
/// </summary>
public class SubscribeOrderBookRequest
{
	public ContractDto Contract { get; set; } = new();
	public int Rows { get; set; } = MarketDepthConstants.DefaultRows;
	public bool PreferSmartDepth { get; set; } = true;
}
