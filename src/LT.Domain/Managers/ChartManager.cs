using LSCore.Exceptions;
using LSCore.Mapper.Domain;
using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Charts;

namespace LT.Domain.Managers;

public class ChartManager(
	IIbkrApiClient ibkrApiClient,
	ICacheManager cacheManager,
	TimeProvider timeProvider
) : IChartManager
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

		var range = ChartPeriodHelpers.GetRange(
			request.Period,
			request.Timeframe,
			timeProvider.GetUtcNow(),
			request.TimeZone
		);
		var result = await cacheManager.GetOrFetchAsync(
			CacheType.ChartBars,
			BarHelpers.GetCacheKey(
				request.Contract.ConId,
				request.Timeframe,
				request.UseRegularTradingHours,
				range.Duration
			),
			ChartTimeframeHelpers.GetCacheTtl(request.Timeframe),
			async ct =>
				(
					await ibkrApiClient.GetHistoricalBarsAsync(
						request.Contract,
						range.Duration,
						ChartTimeframeHelpers.GetBarSize(request.Timeframe),
						ChartTimeframeHelpers.GetWhatToShow(request.Contract.SecurityType),
						request.UseRegularTradingHours,
						ct
					)
				).ToMappedList<BarDto, BarEntity>(),
			cancellationToken
		);
		var bars = result.Value.ToMappedList<BarEntity, BarDto>();
		return new ChartBarsDto
		{
			Bars = range.From is { } from
				? ChartPeriodHelpers.Trim(bars, from, request.Timeframe, request.TimeZone)
				: bars,
			FetchedAt = result.FetchedAt,
			IsStale = result.IsStale,
			Error = result.Error,
			From = range.From,
			LimitedTo = range.LimitedTo,
		};
	}

	public async Task<TradingScheduleDto?> GetRegularHoursAsync(
		ContractDto contract,
		CancellationToken cancellationToken = default
	)
	{
		if (contract.ConId <= 0)
			throw new LSCoreBadRequestException(
				ContractValidationCodes.CTVC_004.GetValidationMessage()
			);

		var result = await cacheManager.GetOrFetchAsync(
			CacheType.Contracts,
			ContractHelpers.GetTradingHoursCacheKey(contract.ConId),
			CacheConstants.TradingHoursTtl,
			async ct =>
				(await ibkrApiClient.SearchContractsAsync(contract, ct)).FirstOrDefault(x =>
					x.Contract.ConId == contract.ConId
				),
			cancellationToken,
			x => x != null
		);
		return result.Value is { } details
			? TradingHoursHelpers.Parse(details.LiquidHours, details.TimeZoneId)
			: null;
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
