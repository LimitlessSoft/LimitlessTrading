using LSCore.Exceptions;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Options;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Options;

namespace LT.Domain.Managers;

/// <summary>
/// Option chain from reqContractDetails (TwsApi has no reqSecDefOptParams): every option of the
/// underlying for one month or one expiration, plus the underlying's current price.
/// </summary>
public class OptionChainManager(IIbkrApiClient ibkrApiClient, IMarketDataManager marketDataManager)
	: IOptionChainManager
{
	public async Task<OptionChainDto> GetChainAsync(
		GetOptionChainRequest request,
		CancellationToken cancellationToken = default
	)
	{
		request.Validate();

		var details = await ibkrApiClient.SearchContractsAsync(
			new ContractDto
			{
				Symbol = request.Underlying.Symbol,
				SecurityType = MarketDataConstants.OptionType,
				Exchange = MarketDataConstants.DefaultExchange,
				Currency = request.Underlying.Currency,
				LastTradeDateOrContractMonth = request.Expiry,
			},
			cancellationToken
		);
		if (details.Count == 0)
			throw new LSCoreBadRequestException(
				OptionChainValidationCodes.OCVC_003.GetValidationMessage(
					request.Underlying.Symbol,
					request.Expiry
				)
			);

		var strikes = OptionChainHelpers.BuildStrikes(details.Select(x => x.Contract));

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
