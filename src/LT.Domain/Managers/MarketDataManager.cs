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
/// Keeps the market data lines in use across the app (quotes here, news ticks through
/// AcquireLine), one registry entry per line, and counts tick-by-tick subscriptions; registered
/// as a singleton (see Program.cs).
/// </summary>
public class MarketDataManager(IIbkrApiClient ibkrApiClient, TimeProvider timeProvider)
	: IMarketDataManager
{
	// Guards _streams: the limit check and the add happen together
	private readonly Lock _lock = new();
	private readonly Dictionary<long, MarketDataStreamDto> _streams = [];
	private long _nextStreamId;
	private int _activeTickByTick;

	public int ActiveLines
	{
		get
		{
			lock (_lock)
				return _streams.Count;
		}
	}

	public int ActiveTickByTick => Volatile.Read(ref _activeTickByTick);

	public event Action? StreamsChanged;

	public IReadOnlyList<MarketDataStreamDto> GetActiveStreams()
	{
		lock (_lock)
			return [.. _streams.Values.OrderBy(x => x.OpenedAt).ThenBy(x => x.Id)];
	}

	public async IAsyncEnumerable<QuoteDto> SubscribeQuotesAsync(
		ContractDto contract,
		[EnumeratorCancellation] CancellationToken cancellationToken = default,
		string genericTicks = "",
		string owner = ""
	)
	{
		if (!ibkrApiClient.IsConnected)
			throw new LSCoreBadRequestException(
				MarketDataValidationCodes.MDVC_002.GetValidationMessage()
			);

		var streamId = AcquireLine(MarketDataStreamKind.Quote, contract, owner, genericTicks);
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
			ReleaseLine(streamId);
		}
	}

	public long AcquireLine(
		MarketDataStreamKind kind,
		ContractDto contract,
		string owner = "",
		string genericTicks = ""
	)
	{
		long id;
		lock (_lock)
		{
			if (_streams.Count >= MarketDataConstants.MaxLines)
				throw new LSCoreBadRequestException(
					MarketDataValidationCodes.MDVC_001.GetValidationMessage(
						MarketDataConstants.MaxLines
					)
				);
			id = ++_nextStreamId;
			_streams[id] = new MarketDataStreamDto
			{
				Id = id,
				Kind = kind,
				Contract = contract,
				GenericTicks = genericTicks,
				Owner = owner,
				OpenedAt = timeProvider.GetUtcNow(),
			};
		}
		StreamsChanged?.Invoke();
		return id;
	}

	public void ReleaseLine(long streamId)
	{
		bool removed;
		lock (_lock)
			removed = _streams.Remove(streamId);
		if (removed)
			StreamsChanged?.Invoke();
	}

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
