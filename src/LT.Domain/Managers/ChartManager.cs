using LSCore.Exceptions;
using LSCore.Mapper.Domain;
using LSCore.Validation.Contracts;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Charts;

namespace LT.Domain.Managers;

public class ChartManager(IIbkrApiClient ibkrApiClient, ICacheManager cacheManager) : IChartManager
{
	public async Task<ChartBarsDto> GetBarsAsync(
		GetBarsRequest request,
		CancellationToken cancellationToken = default
	)
	{
		if (request.Contract.ConId <= 0)
			throw new LSCoreBadRequestException(
				ContractValidationCodes.CTVC_004.GetValidationMessage()
			);

		var result = await cacheManager.GetOrFetchAsync(
			CacheType.ChartBars,
			BarHelpers.GetCacheKey(
				request.Contract.ConId,
				request.Timeframe,
				request.UseRegularTradingHours
			),
			ChartTimeframeHelpers.GetCacheTtl(request.Timeframe),
			async ct =>
				(
					await ibkrApiClient.GetHistoricalBarsAsync(
						request.Contract,
						ChartTimeframeHelpers.GetDuration(request.Timeframe),
						ChartTimeframeHelpers.GetBarSize(request.Timeframe),
						ChartTimeframeHelpers.GetWhatToShow(request.Contract.SecurityType),
						request.UseRegularTradingHours,
						ct
					)
				).ToMappedList<BarDto, BarEntity>(),
			cancellationToken
		);
		return new ChartBarsDto
		{
			Bars = result.Value.ToMappedList<BarEntity, BarDto>(),
			FetchedAt = result.FetchedAt,
			IsStale = result.IsStale,
			Error = result.Error,
		};
	}

	public IAsyncEnumerable<BarDto> SubscribeRealtimeBarsAsync(
		GetBarsRequest request,
		CancellationToken cancellationToken = default
	) =>
		ibkrApiClient.SubscribeRealtimeBarsAsync(
			request.Contract,
			ChartTimeframeHelpers.GetWhatToShow(request.Contract.SecurityType),
			request.UseRegularTradingHours,
			cancellationToken
		);
}
