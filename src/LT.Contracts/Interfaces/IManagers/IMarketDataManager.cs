using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;

namespace LT.Contracts.Interfaces.IManagers;

public interface IMarketDataManager
{
	/// <summary>Market data lines currently in use by this app.</summary>
	int ActiveLines { get; }

	/// <summary>
	/// Streams a cumulative quote after every tick. Counts against the market data line limit
	/// until the enumeration ends.
	/// </summary>
	IAsyncEnumerable<QuoteDto> SubscribeQuotesAsync(
		ContractDto contract,
		CancellationToken cancellationToken = default
	);
}
