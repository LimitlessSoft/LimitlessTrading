using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
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

	/// <summary>
	/// The contract's regular sessions (IB contract details: liquidHours in timeZoneId), cached for
	/// <see cref="Constants.CacheConstants.TradingHoursTtl"/>; null when IB gives none usable.
	/// Tells pre- / post-market bars apart (TradingHoursHelpers.IsRegular).
	/// </summary>
	Task<TradingScheduleDto?> GetRegularHoursAsync(
		ContractDto contract,
		CancellationToken cancellationToken = default
	);

	/// <summary>5-second real-time bars; merge them with BarHelpers.Merge.</summary>
	IAsyncEnumerable<BarDto> SubscribeRealtimeBarsAsync(
		GetBarsRequest request,
		CancellationToken cancellationToken = default
	);
}
