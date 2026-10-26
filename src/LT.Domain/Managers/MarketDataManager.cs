using System.Runtime.CompilerServices;
using LSCore.Exceptions;
using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;

namespace LT.Domain.Managers;

/// <summary>
/// Counts market data lines across the app (quotes here, news ticks through AcquireLine);
/// registered as a singleton (see Program.cs).
/// </summary>
public class MarketDataManager(IIbkrApiClient ibkrApiClient) : IMarketDataManager
{
	private int _activeLines;

	public int ActiveLines => Volatile.Read(ref _activeLines);

	public async IAsyncEnumerable<QuoteDto> SubscribeQuotesAsync(
		ContractDto contract,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		if (!ibkrApiClient.IsConnected)
			throw new LSCoreBadRequestException(
				MarketDataValidationCodes.MDVC_002.GetValidationMessage()
			);

		AcquireLine();
		try
		{
			var quote = new QuoteDto();
			await foreach (
				var tick in ibkrApiClient.SubscribeMarketDataAsync(contract, cancellationToken)
			)
				if (TickTypeHelpers.Apply(quote, tick))
					yield return quote.Clone();
		}
		finally
		{
			ReleaseLine();
		}
	}

	public void AcquireLine()
	{
		if (Interlocked.Increment(ref _activeLines) <= MarketDataConstants.MaxLines)
			return;
		Interlocked.Decrement(ref _activeLines);
		throw new LSCoreBadRequestException(
			MarketDataValidationCodes.MDVC_001.GetValidationMessage(MarketDataConstants.MaxLines)
		);
	}

	public void ReleaseLine() => Interlocked.Decrement(ref _activeLines);
}
