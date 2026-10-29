using System.Runtime.CompilerServices;
using LSCore.Exceptions;
using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IRepositories;
using LT.Contracts.Requests.Charts;

namespace LT.Domain.Managers;

public class FootprintManager(
	IIbkrApiClient ibkrApiClient,
	ICacheManager cacheManager,
	IMarketDataManager marketDataManager,
	IFootprintTapeRepository tapeRepository,
	TimeProvider timeProvider
) : IFootprintManager
{
	public async Task<double> GetTickSizeAsync(
		ContractDto contract,
		CancellationToken cancellationToken = default
	)
	{
		if (contract.ConId <= 0)
			throw new LSCoreBadRequestException(
				ContractValidationCodes.CTVC_004.GetValidationMessage()
			);

		// The same cached contract details the chart reads its trading hours from
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
		return FootprintHelpers.GetTickSize(result.Value?.MinTick ?? 0);
	}

	public async Task<IReadOnlyList<TickByTickDto>> GetTradesAsync(
		GetFootprintTradesRequest request,
		CancellationToken cancellationToken = default
	)
	{
		if (request.Contract.ConId <= 0)
			throw new LSCoreBadRequestException(
				ContractValidationCodes.CTVC_004.GetValidationMessage()
			);
		// Outside regular hours too: what the cache holds is then every trade, for any chart
		var page = await ibkrApiClient
			.GetHistoricalTicksAsync(
				request.Contract,
				HistoricalTickType.Trades,
				FootprintConstants.MaxTicksPerRequest,
				request.Before,
				false,
				cancellationToken
			)
			.ConfigureAwait(false);
		// IB ends a page on a whole second: every trade from its oldest one's second on is in
		var trades = page.Where(x => x.Time < request.Before && x.Price > 0)
			.OrderBy(x => x.Time)
			.Select(x => new FootprintTapeTradeDto(
				x.Time.ToUnixTimeSeconds(),
				x.Price,
				(double)x.Size,
				FootprintSide.Unknown
			))
			.ToList();
		// Up to a little before now: IB's history may still lack the latest trades
		var settled = timeProvider.GetUtcNow() - FootprintConstants.TapeHistoricalSettle;
		var recordTo = request.Before < settled ? request.Before : settled;
		if (trades.Count > 0 && trades[0].Time < recordTo)
			tapeRepository.Append(request.Contract.ConId, trades[0].Time, recordTo, trades);
		return page;
	}

	public Task<IReadOnlyList<FootprintTapeRangeDto>> GetRecordedTradesAsync(
		ContractDto contract,
		DateTimeOffset from,
		DateTimeOffset to,
		CancellationToken cancellationToken = default
	)
	{
		if (contract.ConId <= 0)
			throw new LSCoreBadRequestException(
				ContractValidationCodes.CTVC_004.GetValidationMessage()
			);
		return Task.Run(
			() =>
			{
				var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
				tapeRepository.Prune(
					today.AddDays(-FootprintConstants.TapeRetentionDays),
					FootprintConstants.TapeMaxBytes
				);
				return tapeRepository.Get(contract.ConId, from, to);
			},
			cancellationToken
		);
	}

	public async IAsyncEnumerable<FootprintTradeDto> SubscribeTradesAsync(
		ContractDto contract,
		Action<Exception>? quotesFailed = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		// The latest bid / ask, written by the quote stream and read for each trade
		var quote = new StrongBox<TickByTickDto?>();
		// The trades first: they are what the footprint can't do without
		await using var trades = marketDataManager
			.SubscribeTickByTickAsync(contract, TickByTickType.AllLast, stop.Token)
			.GetAsyncEnumerator(stop.Token);
		Task? quotes = null;
		// The trades recorded to the cache: from the second after the first one (trades of its
		// second may have come before the stream did), in batches of whole seconds
		DateTimeOffset? recordFrom = null;
		var lastTime = DateTimeOffset.MinValue;
		var recording = new List<FootprintTapeTradeDto>();
		try
		{
			double? previousPrice = null;
			var previousSide = FootprintSide.Unknown;
			while (await trades.MoveNextAsync().ConfigureAwait(false))
			{
				var tick = trades.Current;
				// The quotes once the trades are flowing, so a last free subscription goes to
				// the trades
				quotes ??= Task.Run(
					async () =>
					{
						try
						{
							await foreach (
								var bidAsk in marketDataManager
									.SubscribeTickByTickAsync(
										contract,
										TickByTickType.BidAsk,
										stop.Token
									)
									.ConfigureAwait(false)
							)
								if (bidAsk.BidPrice > 0 && bidAsk.AskPrice > 0)
									Volatile.Write(ref quote.Value, bidAsk);
						}
						catch (Exception ex)
						{
							// Ending with the trades (cancelled) is no failure
							Volatile.Write(ref quote.Value, null);
							if (!stop.IsCancellationRequested)
								quotesFailed?.Invoke(ex);
						}
					},
					CancellationToken.None
				);
				if (tick.Price <= 0 || tick.Size <= 0)
					continue;
				var trade = FootprintHelpers.Classify(
					tick,
					Volatile.Read(ref quote.Value),
					previousPrice,
					previousSide
				);
				previousPrice = trade.Price;
				previousSide = trade.Side;
				if (recordFrom is not { } recordStart)
					recordFrom = trade.Time.AddSeconds(1);
				else if (trade.Time >= recordStart)
				{
					// Every trade before this one's second is in (IB sends them in order)
					if (trade.Time - recordStart >= FootprintConstants.TapeLiveFlushInterval)
					{
						tapeRepository.Append(
							contract.ConId,
							recordStart,
							trade.Time,
							FootprintTapeHelpers.TakeBefore(recording, trade.Time)
						);
						recordFrom = trade.Time;
					}
					recording.Add(FootprintTapeHelpers.ToTapeTrade(trade));
					if (trade.Time > lastTime)
						lastTime = trade.Time;
				}
				yield return trade;
			}
		}
		finally
		{
			stop.Cancel();
			// The recording up to the last trade's second (more of it may not have come)
			if (recordFrom is { } from && lastTime > from)
				tapeRepository.Append(
					contract.ConId,
					from,
					lastTime,
					FootprintTapeHelpers.TakeBefore(recording, lastTime)
				);
			if (quotes != null)
				await quotes.ConfigureAwait(false);
		}
	}
}
