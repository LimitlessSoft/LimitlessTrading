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

	/// <summary>
	/// Counts one line for a stream that isn't a quote (news ticks); throws at the limit. Pair
	/// every call with <see cref="ReleaseLine"/>.
	/// </summary>
	void AcquireLine();

	void ReleaseLine();
}
