using System.Runtime.CompilerServices;
using LSCore.Exceptions;
using LSCore.Mapper.Domain;
using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
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
	IMarketDataManager marketDataManager,
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

	public async IAsyncEnumerable<ChartPriceDto> SubscribeLivePricesAsync(
		GetBarsRequest request,
		ChartLiveUpdateMode mode,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		if (!ChartLiveUpdateHelpers.UsesLivePrices(mode))
			yield break;

		if (ChartLiveUpdateHelpers.UsesTickByTick(mode))
		{
			// Through the market data manager: it counts IB's 5 tick-by-tick subscriptions
			await foreach (
				var tick in marketDataManager.SubscribeTickByTickAsync(
					request.Contract,
					ChartLiveUpdateHelpers.GetTickByTickType(request.Contract.SecurityType),
					cancellationToken
				)
			)
				if (tick.Price > 0)
					yield return new ChartPriceDto { Time = tick.Time, Price = tick.Price };
			yield break;
		}

		// Through the market data manager: the chart's quote line counts against the limit
		QuoteDto? previous = null;
		await foreach (
			var quote in marketDataManager.SubscribeQuotesAsync(request.Contract, cancellationToken)
		)
		{
			var price = ChartLiveUpdateHelpers.GetLivePrice(
				quote,
				previous,
				request.Contract.SecurityType
			);
			previous = quote;
			if (price is { } value)
				yield return new ChartPriceDto { Time = timeProvider.GetUtcNow(), Price = value };
		}
	}
}
