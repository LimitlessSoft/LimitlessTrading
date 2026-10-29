using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Requests.Charts;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>
/// The trades behind a chart's footprint bars (<see cref="Enums.ChartBarStyle.Footprint"/>):
/// past ones a page at a time (IB historical ticks), live ones with the side that started them.
/// </summary>
public interface IFootprintManager
{
	/// <summary>
	/// The contract's price step for footprint rows: its minimum tick from IB's contract details
	/// (cached with its trading hours), <see cref="Constants.FootprintConstants.FallbackTickSize"/>
	/// when IB gives none.
	/// </summary>
	Task<double> GetTickSizeAsync(
		ContractDto contract,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// One page of past trades strictly before <see cref="GetFootprintTradesRequest.Before"/>,
	/// oldest first (about <see cref="Constants.FootprintConstants.MaxTicksPerRequest"/>, whole
	/// seconds; empty where IB has none), outside regular trading hours too. Every call is one
	/// IB request (one at a time, ~22 a minute); the page is recorded to the footprint trade
	/// cache (<see cref="GetRecordedTradesAsync"/>) as every trade from its oldest one's second
	/// to <see cref="GetFootprintTradesRequest.Before"/>, at most up to
	/// <see cref="Constants.FootprintConstants.TapeHistoricalSettle"/> before now.
	/// </summary>
	Task<IReadOnlyList<TickByTickDto>> GetTradesAsync(
		GetFootprintTradesRequest request,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// What the footprint trade cache holds of the UTC days from <paramref name="from"/> to
	/// before <paramref name="to"/> (past pages and live trades recorded earlier, by any chart):
	/// the stretches it has every trade of, oldest first. Read off the calling thread; prunes the
	/// cache first (<see cref="Constants.FootprintConstants.TapeRetentionDays"/>,
	/// <see cref="Constants.FootprintConstants.TapeMaxBytes"/>).
	/// </summary>
	Task<IReadOnlyList<FootprintTapeRangeDto>> GetRecordedTradesAsync(
		ContractDto contract,
		DateTimeOffset from,
		DateTimeOffset to,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Live trades (tick-by-tick AllLast, odd lots included like the past trades), each with its
	/// side by the quote rule against a tick-by-tick BidAsk stream
	/// (<c>FootprintHelpers.Classify</c>). Takes two of IB's
	/// <see cref="Constants.MarketDataConstants.MaxTickByTick"/> tick-by-tick subscriptions through
	/// <see cref="IMarketDataManager"/>. Without a trade subscription (limit reached, IB refusal)
	/// the stream fails with that error; when only the quotes fail,
	/// <paramref name="quotesFailed"/> is called with the error (on a thread-pool thread) and
	/// sides go by the tick rule from then on. The trades are recorded to the footprint trade
	/// cache every <see cref="Constants.FootprintConstants.TapeLiveFlushInterval"/> (and when the
	/// stream ends), from the second after the first one to the last one's second.
	/// </summary>
	IAsyncEnumerable<FootprintTradeDto> SubscribeTradesAsync(
		ContractDto contract,
		Action<Exception>? quotesFailed = null,
		CancellationToken cancellationToken = default
	);
}
