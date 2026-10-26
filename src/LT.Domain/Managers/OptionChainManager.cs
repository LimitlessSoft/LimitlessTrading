using LSCore.Exceptions;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Options;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Options;

namespace LT.Domain.Managers;

/// <summary>
/// Expirations from reqSecDefOptParams; the chain of one expiration (or month) from
/// reqContractDetails, so only strikes that exist that day are listed, plus the underlying's price.
/// Expirations and strike lists are cached (CacheType.OptionChains) for a day; the underlying's
/// price is always asked live.
/// </summary>
public class OptionChainManager(
	IIbkrApiClient ibkrApiClient,
	IMarketDataManager marketDataManager,
	ICacheManager cacheManager
) : IOptionChainManager
{
	public async Task<IReadOnlyList<OptionExpirationDto>> GetExpirationsAsync(
		ContractDto underlying,
		CancellationToken cancellationToken = default
	)
	{
		if (!ContractHelpers.SupportsOptionChain(underlying))
			throw new LSCoreBadRequestException(
				OptionChainValidationCodes.OCVC_002.GetValidationMessage()
			);
		if (underlying.ConId <= 0)
			throw new LSCoreBadRequestException(
				ContractValidationCodes.CTVC_004.GetValidationMessage()
			);

		var cached = await cacheManager.GetOrFetchAsync(
			CacheType.OptionChains,
			OptionChainHelpers.GetExpirationsCacheKey(underlying),
			CacheConstants.OptionChainTtl,
			async ct =>
				OptionChainHelpers
					.SelectExpirations(
						underlying.Symbol,
						await ibkrApiClient.GetOptionChainParametersAsync(underlying, ct)
					)
					.ToList(),
			cancellationToken,
			x => x.Count > 0
		);
		var expirations = cached.Value;
		if (expirations.Count == 0)
			throw new LSCoreBadRequestException(
				OptionChainValidationCodes.OCVC_004.GetValidationMessage(underlying.Symbol)
			);
		return expirations;
	}

	public async Task<OptionChainDto> GetChainAsync(
		GetOptionChainRequest request,
		CancellationToken cancellationToken = default
	)
	{
		request.Validate();

		// Only the structure (which contracts exist) is cached, never a price
		var cached = await cacheManager.GetOrFetchAsync(
			CacheType.OptionChains,
			OptionChainHelpers.GetStrikesCacheKey(request),
			CacheConstants.OptionChainTtl,
			async ct =>
				OptionChainHelpers
					.BuildStrikes(
						(
							await ibkrApiClient.SearchContractsAsync(
								new ContractDto
								{
									Symbol = request.Underlying.Symbol,
									SecurityType = MarketDataConstants.OptionType,
									Exchange = MarketDataConstants.DefaultExchange,
									Currency = request.Underlying.Currency,
									LastTradeDateOrContractMonth = request.Expiry,
									TradingClass = request.TradingClass,
								},
								ct
							)
						).Select(x => x.Contract)
					)
					.ToList(),
			cancellationToken,
			x => x.Count > 0
		);
		var strikes = cached.Value;
		if (strikes.Count == 0)
			throw new LSCoreBadRequestException(
				OptionChainValidationCodes.OCVC_003.GetValidationMessage(
					request.Underlying.Symbol,
					request.Expiry
				)
			);

		double? underlyingPrice = null;
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(MarketDataConstants.UnderlyingPriceTimeout);
		try
		{
			await foreach (
				var quote in marketDataManager.SubscribeQuotesAsync(
					request.Underlying,
					timeout.Token
				)
			)
			{
				underlyingPrice = TickTypeHelpers.GetDisplayPrice(quote);
				if (quote.Last != null)
					break;
			}
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			// no trade within the timeout - fall back to the close (or the middle strike)
		}

		return new OptionChainDto
		{
			Underlying = request.Underlying,
			UnderlyingPrice = underlyingPrice,
			Expirations = strikes.Select(x => x.Expiration).Distinct().ToList(),
			Strikes = strikes,
		};
	}
}
