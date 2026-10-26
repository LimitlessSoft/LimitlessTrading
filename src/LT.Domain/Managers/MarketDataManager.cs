using System.Runtime.CompilerServices;
using LSCore.Exceptions;
using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;

namespace LT.Domain.Managers;

/// <summary>
/// Counts market data lines across the app (quotes here, news ticks through AcquireLine) and
/// tick-by-tick subscriptions; registered as a singleton (see Program.cs).
/// </summary>
public class MarketDataManager(IIbkrApiClient ibkrApiClient) : IMarketDataManager
{
	private int _activeLines;
	private int _activeTickByTick;

	public int ActiveLines => Volatile.Read(ref _activeLines);
	public int ActiveTickByTick => Volatile.Read(ref _activeTickByTick);

	public async IAsyncEnumerable<QuoteDto> SubscribeQuotesAsync(
		ContractDto contract,
		[EnumeratorCancellation] CancellationToken cancellationToken = default,
		string genericTicks = ""
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
				var tick in ibkrApiClient.SubscribeMarketDataAsync(
					contract,
					cancellationToken,
					genericTicks
				)
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

	public async IAsyncEnumerable<TickByTickDto> SubscribeTickByTickAsync(
		ContractDto contract,
		TickByTickType tickType,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		if (!ibkrApiClient.IsConnected)
			throw new LSCoreBadRequestException(
				MarketDataValidationCodes.MDVC_002.GetValidationMessage()
			);
		if (Interlocked.Increment(ref _activeTickByTick) > MarketDataConstants.MaxTickByTick)
		{
			Interlocked.Decrement(ref _activeTickByTick);
			throw new LSCoreBadRequestException(
				MarketDataValidationCodes.MDVC_003.GetValidationMessage(
					MarketDataConstants.MaxTickByTick
				)
			);
		}
		try
		{
			await foreach (
				var tick in ibkrApiClient.SubscribeTickByTickAsync(
					contract,
					tickType,
					cancellationToken
				)
			)
				yield return tick;
		}
		finally
		{
			Interlocked.Decrement(ref _activeTickByTick);
		}
	}
}
