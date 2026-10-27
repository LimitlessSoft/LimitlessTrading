using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;

namespace LT.Contracts.Interfaces.IManagers;

public interface IMarketDataManager
{
	/// <summary>Market data lines currently in use by this app.</summary>
	int ActiveLines { get; }

	/// <summary>The lines in use (one entry per line), oldest first; a snapshot.</summary>
	IReadOnlyList<MarketDataStreamDto> GetActiveStreams();

	/// <summary>
	/// Raised after a line is taken or released, on the thread that did it (often the thread
	/// pool): post to the UI thread before touching bound state.
	/// </summary>
	event Action? StreamsChanged;

	/// <summary>
	/// Streams a cumulative quote after every tick. Counts against the market data line limit
	/// until the enumeration ends. <paramref name="genericTicks"/> asks for extra IB generic
	/// ticks on the same line (e.g. <c>MarketDataConstants.OptionImpliedVolatilityGenericTick</c>).
	/// <paramref name="owner"/> labels the line in the stream list
	/// (<c>MarketDataStreamOwners</c>).
	/// </summary>
	IAsyncEnumerable<QuoteDto> SubscribeQuotesAsync(
		ContractDto contract,
		CancellationToken cancellationToken = default,
		string genericTicks = "",
		string owner = ""
	);

	/// <summary>
	/// Takes one line for a stream that isn't a quote (news ticks) and lists it; throws at the
	/// limit. Returns the stream id: pass it to <see cref="ReleaseLine"/> exactly once.
	/// <paramref name="genericTicks"/> is only recorded for the list.
	/// </summary>
	long AcquireLine(
		MarketDataStreamKind kind,
		ContractDto contract,
		string owner = "",
		string genericTicks = ""
	);

	/// <summary>Releases a line taken with <see cref="AcquireLine"/>; an unknown id is ignored.</summary>
	void ReleaseLine(long streamId);

	/// <summary>Tick-by-tick subscriptions currently in use by this app.</summary>
	int ActiveTickByTick { get; }

	/// <summary>
	/// IB tick-by-tick data; counts against IB's limit of
	/// <c>MarketDataConstants.MaxTickByTick</c> until the enumeration ends and throws at it.
	/// </summary>
	IAsyncEnumerable<TickByTickDto> SubscribeTickByTickAsync(
		ContractDto contract,
		TickByTickType tickType,
		CancellationToken cancellationToken = default
	);
}
