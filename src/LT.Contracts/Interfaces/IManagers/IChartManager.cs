using LT.Contracts.Dtos.Charts;
using LT.Contracts.Enums;
using LT.Contracts.Requests.Charts;

namespace LT.Contracts.Interfaces.IManagers;

public interface IChartManager
{
	/// <summary>
	/// Historical bars of the request's period (ending now), served from the local cache while it
	/// is fresh. A period longer than the timeframe loads is cut to its longest
	/// (<see cref="Dtos.Charts.ChartBarsDto.LimitedTo"/>).
	/// </summary>
	Task<ChartBarsDto> GetBarsAsync(
		GetBarsRequest request,
		CancellationToken cancellationToken = default
	);

	/// <summary>5-second real-time bars; merge them with BarHelpers.Merge.</summary>
	IAsyncEnumerable<BarDto> SubscribeRealtimeBarsAsync(
		GetBarsRequest request,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Live prices that move the last bar between 5-second bars (merge them with
	/// BarHelpers.MergePrice), one per trade (midpoint for forex). Quote modes: the quote stream's
	/// last trade, stamped on arrival, on one market data line. Tick-by-tick modes: IB's
	/// tick-by-tick trades (MidPoint for forex), stamped by IB, on one of IB's 5 tick-by-tick
	/// subscriptions. Ends at once for <see cref="ChartLiveUpdateMode.RealtimeBars"/>. No
	/// fallback here: a failure (no line or subscription left, IB refusal) ends the stream with
	/// the error; see ChartLiveUpdateHelpers.GetFallback.
	/// </summary>
	IAsyncEnumerable<ChartPriceDto> SubscribeLivePricesAsync(
		GetBarsRequest request,
		ChartLiveUpdateMode mode,
		CancellationToken cancellationToken = default
	);
}
