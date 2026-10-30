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
	/// Streams a cumulative quote after every tick. Subscriptions of the same contract and
	/// <paramref name="genericTicks"/> share one IB line: the first opens it (and counts against
	/// the market data line limit, throwing at it), later ones get the latest quote at once and
	/// then every update, and the last one to leave cancels it. Cancelling one subscription
	/// leaves the others streaming; an IB error ends all of them with it.
	/// <paramref name="genericTicks"/> asks for extra IB generic ticks on the same line (e.g.
	/// <c>MarketDataConstants.OptionImpliedVolatilityGenericTick</c>). <paramref name="owner"/>
	/// labels the line in the stream list (<c>MarketDataStreamOwners</c>); a shared line lists
	/// every owner.
	/// </summary>
	IAsyncEnumerable<QuoteDto> SubscribeQuotesAsync(
		ContractDto contract,
		CancellationToken cancellationToken = default,
		string genericTicks = "",
		string owner = ""
	);

	/// <summary>
	/// One quote of a contract, from an IB snapshot (no market data line: IB doesn't count
	/// snapshots against the limit), or the latest quote of a line already streaming it. IB ends
	/// a snapshot about 11 s after the request; it fails after
	/// <c>MarketDataConstants.SnapshotTimeout</c>, and at most
	/// <c>MarketDataConstants.MaxConcurrentSnapshots</c> run at once (more throw).
	/// </summary>
	Task<QuoteDto> GetQuoteSnapshotAsync(
		ContractDto contract,
		CancellationToken cancellationToken = default
	);

	/// <summary>Quote snapshots running now.</summary>
	int ActiveSnapshots { get; }

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

	/// <summary>
	/// IB tick-by-tick subscriptions currently open by this app: one per shared stream, however
	/// many subscribers read it.
	/// </summary>
	int ActiveTickByTick { get; }

	/// <summary>
	/// IB tick-by-tick data. Subscriptions of the same contract and <paramref name="tickType"/>
	/// share one IB subscription: the first opens it (and counts against IB's limit of
	/// <c>MarketDataConstants.MaxTickByTick</c>, throwing at it), later ones get every tick from
	/// the moment they join (no replay of earlier ticks), and the last one to leave cancels it.
	/// Cancelling one subscription leaves the others streaming; an IB error or end ends all of
	/// them, with the slot already free. Every subscriber gets the same tick objects: read only.
	/// </summary>
	IAsyncEnumerable<TickByTickDto> SubscribeTickByTickAsync(
		ContractDto contract,
		TickByTickType tickType,
		CancellationToken cancellationToken = default
	);
}
