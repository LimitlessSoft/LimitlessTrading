using LT.Contracts.Dtos.Charts;
using LT.Contracts.Requests.Charts;

namespace LT.Contracts.Interfaces.IManagers;

public interface IChartManager
{
	/// <summary>Historical bars, served from the local cache while it is fresh.</summary>
	Task<ChartBarsDto> GetBarsAsync(
		GetBarsRequest request,
		CancellationToken cancellationToken = default
	);

	/// <summary>5-second real-time bars; merge them with BarHelpers.Merge.</summary>
	IAsyncEnumerable<BarDto> SubscribeRealtimeBarsAsync(
		GetBarsRequest request,
		CancellationToken cancellationToken = default
	);
}
