using LSCore.Exceptions;
using LSCore.Mapper.Domain;
using LSCore.Validation.Contracts;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Entities;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IRepositories;
using LT.Contracts.Requests.Charts;

namespace LT.Domain.Managers;

public class ChartManager(
	IIbkrApiClient ibkrApiClient,
	IBarCacheRepository barCacheRepository,
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

		var key = BarHelpers.GetCacheKey(
			request.Contract.ConId,
			request.Timeframe,
			request.UseRegularTradingHours
		);
		var cached = barCacheRepository.Get(key);
		var now = timeProvider.GetUtcNow();
		if (
			cached != null
			&& now - cached.FetchedAt < ChartTimeframeHelpers.GetCacheTtl(request.Timeframe)
		)
			return new ChartBarsDto
			{
				Bars = cached.Bars.ToMappedList<BarEntity, BarDto>(),
				FetchedAt = cached.FetchedAt,
			};

		try
		{
			var bars = await ibkrApiClient.GetHistoricalBarsAsync(
				request.Contract,
				ChartTimeframeHelpers.GetDuration(request.Timeframe),
				ChartTimeframeHelpers.GetBarSize(request.Timeframe),
				ChartTimeframeHelpers.GetWhatToShow(request.Contract.SecurityType),
				request.UseRegularTradingHours,
				cancellationToken
			);
			barCacheRepository.Save(
				new BarCacheEntity
				{
					Key = key,
					FetchedAt = now,
					Bars = bars.ToMappedList<BarDto, BarEntity>(),
				}
			);
			return new ChartBarsDto { Bars = bars, FetchedAt = now };
		}
		catch (Exception ex) when (cached != null && ex is not OperationCanceledException)
		{
			return new ChartBarsDto
			{
				Bars = cached.Bars.ToMappedList<BarEntity, BarDto>(),
				FetchedAt = cached.FetchedAt,
				IsStale = true,
				Error = ex.Message,
			};
		}
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
