using System.Runtime.CompilerServices;
using LSCore.Exceptions;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.MarketDepth;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Exceptions;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.MarketDepth;

namespace LT.Domain.Managers;

/// <summary>
/// Keeps order books from IB depth updates and counts depth subscriptions against IB's small
/// limit; registered as a singleton (see Program.cs).
/// </summary>
public class MarketDepthManager(IIbkrApiClient ibkrApiClient) : IMarketDepthManager
{
	private int _active;

	public int ActiveSubscriptions => Volatile.Read(ref _active);

	public async IAsyncEnumerable<OrderBookDto> SubscribeOrderBookAsync(
		SubscribeOrderBookRequest request,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		request.Validate();
		if (!ibkrApiClient.IsConnected)
			throw new LSCoreBadRequestException(
				MarketDataValidationCodes.MDVC_002.GetValidationMessage()
			);

		var (contract, isSmartDepth) = OrderBookHelpers.GetDepthContract(
			request.Contract,
			request.PreferSmartDepth
		);
		if (Interlocked.Increment(ref _active) > MarketDepthConstants.MaxSubscriptions)
		{
			Interlocked.Decrement(ref _active);
			throw new LSCoreBadRequestException(
				MarketDepthValidationCodes.MDPVC_002.GetValidationMessage(
					MarketDepthConstants.MaxSubscriptions
				)
			);
		}
		try
		{
			var bids = new List<OrderBookLevelDto>();
			var asks = new List<OrderBookLevelDto>();
			await using var updates = ibkrApiClient
				.SubscribeMarketDepthAsync(contract, request.Rows, isSmartDepth, cancellationToken)
				.GetAsyncEnumerator(cancellationToken);
			while (true)
			{
				try
				{
					if (!await updates.MoveNextAsync())
						break;
				}
				catch (IbkrRequestException ex)
					when (OrderBookHelpers.GetErrorMessage(ex.Code, contract) is { } message)
				{
					throw new LSCoreBadRequestException(message);
				}
				if (OrderBookHelpers.Apply(bids, asks, updates.Current, request.Rows))
					yield return OrderBookHelpers.Snapshot(bids, asks, isSmartDepth);
			}
		}
		finally
		{
			Interlocked.Decrement(ref _active);
		}
	}
}
