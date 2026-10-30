using System.Runtime.ExceptionServices;
using LSCore.Exceptions;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.GammaExposure;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.GammaExposure;
using LT.Contracts.Requests.Options;

namespace LT.Domain.Managers;

/// <summary>
/// Gamma exposure of one expiration: the chain and the underlying's price from
/// <see cref="IOptionChainManager"/>, then each call and put around the price streamed through
/// <see cref="IMarketDataManager"/> (generic tick 101, so its line is counted and shared) only
/// until IB's model gamma and its open interest came. Nothing is cached: gamma moves with the
/// price.
/// </summary>
public class GammaExposureManager(
	IOptionChainManager optionChainManager,
	IMarketDataManager marketDataManager,
	TimeProvider timeProvider
) : IGammaExposureManager
{
	public async Task<GammaExposureDto> GetGammaExposureAsync(
		GetGammaExposureRequest request,
		IProgress<GammaExposureProgressDto>? progress = null,
		CancellationToken cancellationToken = default
	)
	{
		request.Validate();

		var chain = await optionChainManager.GetChainAsync(
			new GetOptionChainRequest
			{
				Underlying = request.Underlying,
				Expiry = request.Expiration,
				TradingClass = request.TradingClass,
			},
			cancellationToken
		);
		if (chain.UnderlyingPrice is not { } spot || spot <= 0)
			throw new LSCoreBadRequestException(
				GammaExposureValidationCodes.GXVC_004.GetValidationMessage(
					request.Underlying.Symbol
				)
			);

		var strikes = OptionChainHelpers.AroundPrice(
			[.. chain.Strikes.Where(x => x.Expiration == request.Expiration)],
			spot,
			request.StrikesAroundSpot
		);
		var contracts = GammaExposureHelpers.GetOptionContracts(strikes);
		var budget = GammaExposureHelpers.GetLineBudget(marketDataManager.ActiveLines);
		if (budget == 0)
			throw new LSCoreBadRequestException(
				GammaExposureValidationCodes.GXVC_003.GetValidationMessage(
					MarketDataConstants.MaxLines
				)
			);

		var options = new GammaExposureOptionDto[contracts.Count];
		Exception? firstError = null;
		var done = 0;
		progress?.Report(new GammaExposureProgressDto(0, contracts.Count));
		await Parallel.ForEachAsync(
			Enumerable.Range(0, contracts.Count),
			new ParallelOptions
			{
				MaxDegreeOfParallelism = budget,
				CancellationToken = cancellationToken,
			},
			async (i, token) =>
			{
				try
				{
					options[i] = await GammaExposureHelpers.ReadOptionAsync(
						marketDataManager,
						contracts[i],
						GammaExposureConstants.OptionReadTimeout,
						timeProvider,
						token
					);
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					// A refused line (no options data, the line limit) counts as no answer
					Interlocked.CompareExchange(ref firstError, ex, null);
					options[i] = GammaExposureHelpers.ToOption(contracts[i], null);
				}
				progress?.Report(
					new GammaExposureProgressDto(Interlocked.Increment(ref done), contracts.Count)
				);
			}
		);

		var result = GammaExposureHelpers.Build(
			chain.Underlying,
			request.Expiration,
			spot,
			options,
			GammaExposureHelpers.GetMultiplier(contracts.FirstOrDefault()),
			timeProvider.GetUtcNow()
		);
		if (result.OptionsReceived > 0)
			return result;
		if (firstError != null)
			ExceptionDispatchInfo.Throw(firstError);
		throw new LSCoreBadRequestException(
			GammaExposureValidationCodes.GXVC_005.GetValidationMessage(
				contracts.Count,
				request.Underlying.Symbol
			)
		);
	}
}
