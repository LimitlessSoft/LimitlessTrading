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
using LT.Contracts.Requests.Charts;

namespace LT.Domain.Managers;

public class FootprintManager(
	IIbkrApiClient ibkrApiClient,
	ICacheManager cacheManager,
	IMarketDataManager marketDataManager
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

	public Task<IReadOnlyList<TickByTickDto>> GetTradesAsync(
		GetFootprintTradesRequest request,
		CancellationToken cancellationToken = default
	)
	{
		if (request.Contract.ConId <= 0)
			throw new LSCoreBadRequestException(
				ContractValidationCodes.CTVC_004.GetValidationMessage()
			);
		return ibkrApiClient.GetHistoricalTicksAsync(
			request.Contract,
			HistoricalTickType.Trades,
			FootprintConstants.MaxTicksPerRequest,
			request.Before,
			request.UseRegularTradingHours,
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
				yield return trade;
			}
		}
		finally
		{
			stop.Cancel();
			if (quotes != null)
				await quotes.ConfigureAwait(false);
		}
	}
}
