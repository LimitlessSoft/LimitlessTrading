using System.Runtime.CompilerServices;
using System.Threading.Channels;
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
/// AcquireLine), one registry entry per line, and counts tick-by-tick subscriptions and quote
/// snapshots; registered as a singleton (see Program.cs). Quote subscriptions of the same contract
/// and generic ticks share one IB line, and tick-by-tick subscriptions of the same contract and
/// tick type one IB tick-by-tick stream, each fanned out to every subscriber.
/// </summary>
public class MarketDataManager(IIbkrApiClient ibkrApiClient, TimeProvider timeProvider)
	: IMarketDataManager
{
	// Guards _streams, _shared and _sharedTickByTick: the limit check and the add happen
	// together, and a shared stream's subscribers, latest quote and fan-out change together
	private readonly Lock _lock = new();
	private readonly Dictionary<long, MarketDataStreamDto> _streams = [];
	private readonly Dictionary<string, SharedQuoteLine> _shared = [];

	// One entry per IB tick-by-tick subscription: what counts against IB's limit
	private readonly Dictionary<string, SharedTickByTickStream> _sharedTickByTick = [];
	private long _nextStreamId;
	private int _activeSnapshots;

	public int ActiveLines
	{
		get
		{
			lock (_lock)
				return _streams.Count;
		}
	}

	public int ActiveTickByTick
	{
		get
		{
			lock (_lock)
				return _sharedTickByTick.Count;
		}
	}

	public int ActiveSnapshots => Volatile.Read(ref _activeSnapshots);

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

		var key = MarketDataShareHelpers.GetShareKey(
			contract,
			genericTicks,
			ibkrApiClient.MarketDataType
		);
		var subscriber = new QuoteSubscriber(owner);
		SharedQuoteLine line;
		var opened = false;
		lock (_lock)
		{
			if (_shared.TryGetValue(key, out var existing))
			{
				line = existing;
				line.Subscribers.Add(subscriber);
				// Joins with the latest quote; the pump writes every later one under this lock
				if (line.Latest != null)
					subscriber.Quotes.Writer.TryWrite(line.Latest.Clone());
				UpdateStream(line);
			}
			else
			{
				var streamId = AddStream(MarketDataStreamKind.Quote, contract, owner, genericTicks);
				line = new SharedQuoteLine(key, streamId, contract, genericTicks);
				line.Subscribers.Add(subscriber);
				_shared[key] = line;
				opened = true;
			}
		}
		try
		{
			StreamsChanged?.Invoke();
			if (opened)
				_ = Task.Run(() => PumpAsync(line));
			await foreach (
				var quote in subscriber
					.Quotes.Reader.ReadAllAsync(cancellationToken)
					.ConfigureAwait(false)
			)
				yield return quote;
		}
		finally
		{
			Leave(line, subscriber);
		}
	}

	public async Task<QuoteDto> GetQuoteSnapshotAsync(
		ContractDto contract,
		CancellationToken cancellationToken = default
	)
	{
		if (!ibkrApiClient.IsConnected)
			throw new LSCoreBadRequestException(
				MarketDataValidationCodes.MDVC_002.GetValidationMessage()
			);

		// A line already streaming it knows better than a snapshot
		var key = MarketDataShareHelpers.GetShareKey(contract, "", ibkrApiClient.MarketDataType);
		lock (_lock)
			if (_shared.TryGetValue(key, out var line) && line.Latest != null)
				return line.Latest.Clone();

		if (
			Interlocked.Increment(ref _activeSnapshots) > MarketDataConstants.MaxConcurrentSnapshots
		)
		{
			Interlocked.Decrement(ref _activeSnapshots);
			throw new LSCoreBadRequestException(
				MarketDataValidationCodes.MDVC_004.GetValidationMessage(
					MarketDataConstants.MaxConcurrentSnapshots
				)
			);
		}
		using var timeout = new CancellationTokenSource(
			MarketDataConstants.SnapshotTimeout,
			timeProvider
		);
		using var linked = CancellationTokenSource.CreateLinkedTokenSource(
			cancellationToken,
			timeout.Token
		);
		try
		{
			var ticks = await ibkrApiClient
				.GetMarketDataSnapshotAsync(contract, linked.Token)
				.ConfigureAwait(false);
			var quote = new QuoteDto();
			foreach (var tick in ticks)
				TickTypeHelpers.Apply(quote, tick);
			return quote;
		}
		catch (OperationCanceledException)
			when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			throw new LSCoreBadRequestException(
				MarketDataValidationCodes.MDVC_005.GetValidationMessage()
			);
		}
		finally
		{
			Interlocked.Decrement(ref _activeSnapshots);
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
			id = AddStream(kind, contract, owner, genericTicks);
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

		var key = MarketDataShareHelpers.GetTickByTickShareKey(contract, tickType);
		var subscriber = new TickByTickSubscriber();
		SharedTickByTickStream stream;
		var opened = false;
		lock (_lock)
		{
			if (_sharedTickByTick.TryGetValue(key, out var existing))
				stream = existing;
			else
			{
				// Only a new IB subscription takes one of IB's few
				if (_sharedTickByTick.Count >= MarketDataConstants.MaxTickByTick)
					throw new LSCoreBadRequestException(
						MarketDataValidationCodes.MDVC_003.GetValidationMessage(
							MarketDataConstants.MaxTickByTick
						)
					);
				stream = new SharedTickByTickStream(key, contract, tickType);
				_sharedTickByTick[key] = stream;
				opened = true;
			}
			stream.Subscribers.Add(subscriber);
		}
		try
		{
			if (opened)
				_ = Task.Run(() => PumpTickByTickAsync(stream));
			await foreach (
				var tick in subscriber
					.Ticks.Reader.ReadAllAsync(cancellationToken)
					.ConfigureAwait(false)
			)
				yield return tick;
		}
		finally
		{
			LeaveTickByTick(stream, subscriber);
		}
	}

	// Under _lock: takes a line, or throws at the limit
	private long AddStream(
		MarketDataStreamKind kind,
		ContractDto contract,
		string owner,
		string genericTicks
	)
	{
		if (_streams.Count >= MarketDataConstants.MaxLines)
			throw new LSCoreBadRequestException(
				MarketDataValidationCodes.MDVC_001.GetValidationMessage(
					MarketDataConstants.MaxLines
				)
			);
		var id = ++_nextStreamId;
		_streams[id] = new MarketDataStreamDto
		{
			Id = id,
			Kind = kind,
			Contract = contract,
			GenericTicks = genericTicks,
			Owner = owner,
			Owners = MarketDataShareHelpers.GetOwners([owner]),
			OpenedAt = timeProvider.GetUtcNow(),
		};
		return id;
	}

	// Under _lock: the line's list entry follows its subscribers (a new object, so the stream
	// list sees the change)
	private void UpdateStream(SharedQuoteLine line)
	{
		if (_streams.TryGetValue(line.StreamId, out var stream))
			_streams[line.StreamId] = MarketDataShareHelpers.WithSubscribers(
				stream,
				line.Subscribers.Select(x => x.Owner)
			);
	}

	// One subscriber is done (read to the end, stopped or cancelled): the last one closes the
	// line and cancels its IB stream (outside the lock: cancelling runs the stream's callbacks)
	private void Leave(SharedQuoteLine line, QuoteSubscriber subscriber)
	{
		var cancel = false;
		lock (_lock)
		{
			if (!line.Subscribers.Remove(subscriber) || line.IsClosed)
				return;
			if (line.Subscribers.Count == 0)
				cancel = Close(line);
			else
				UpdateStream(line);
		}
		if (cancel)
			line.Cancel.Cancel();
		StreamsChanged?.Invoke();
	}

	// Under _lock: the line leaves the list and new subscribers open a fresh one; true the first
	// time only
	private bool Close(SharedQuoteLine line)
	{
		if (line.IsClosed)
			return false;
		line.IsClosed = true;
		_shared.Remove(line.Key);
		_streams.Remove(line.StreamId);
		return true;
	}

	// The IB stream of a shared line: every quote to every subscriber; its end or error ends them
	private async Task PumpAsync(SharedQuoteLine line)
	{
		Exception? error = null;
		try
		{
			var quote = new QuoteDto();
			await foreach (
				var tick in ibkrApiClient
					.SubscribeMarketDataAsync(line.Contract, line.Cancel.Token, line.GenericTicks)
					.ConfigureAwait(false)
			)
			{
				if (!TickTypeHelpers.Apply(quote, tick))
					continue;
				lock (_lock)
				{
					line.Latest = quote.Clone();
					foreach (var subscriber in line.Subscribers)
						subscriber.Quotes.Writer.TryWrite(line.Latest.Clone());
				}
			}
		}
		catch (OperationCanceledException) when (line.Cancel.IsCancellationRequested) { }
		catch (Exception ex)
		{
			error = ex;
		}

		// IB ended it (or failed it) with subscribers still reading: the line is gone for all
		bool closed;
		lock (_lock)
		{
			closed = Close(line);
			foreach (var subscriber in line.Subscribers)
				subscriber.Quotes.Writer.TryComplete(error);
		}
		if (closed)
			StreamsChanged?.Invoke();
	}

	// One tick-by-tick subscriber is done: the last one ends the IB subscription (outside the
	// lock: cancelling runs the stream's callbacks)
	private void LeaveTickByTick(SharedTickByTickStream stream, TickByTickSubscriber subscriber)
	{
		bool cancel;
		lock (_lock)
		{
			if (!stream.Subscribers.Remove(subscriber) || stream.Subscribers.Count > 0)
				return;
			cancel = CloseTickByTick(stream);
		}
		if (cancel)
			stream.Cancel.Cancel();
	}

	// Under _lock: frees the stream's slot and new subscribers open a fresh one; true the first
	// time only
	private bool CloseTickByTick(SharedTickByTickStream stream)
	{
		if (stream.IsClosed)
			return false;
		stream.IsClosed = true;
		_sharedTickByTick.Remove(stream.Key);
		return true;
	}

	// The IB subscription of a shared tick-by-tick stream: every tick to every subscriber; its
	// end or error ends them (the slot is free before they see it)
	private async Task PumpTickByTickAsync(SharedTickByTickStream stream)
	{
		Exception? error = null;
		try
		{
			await foreach (
				var tick in ibkrApiClient
					.SubscribeTickByTickAsync(stream.Contract, stream.TickType, stream.Cancel.Token)
					.ConfigureAwait(false)
			)
				lock (_lock)
					foreach (var subscriber in stream.Subscribers)
						subscriber.Ticks.Writer.TryWrite(tick);
		}
		catch (OperationCanceledException) when (stream.Cancel.IsCancellationRequested) { }
		catch (Exception ex)
		{
			error = ex;
		}

		lock (_lock)
		{
			CloseTickByTick(stream);
			foreach (var subscriber in stream.Subscribers)
				subscriber.Ticks.Writer.TryComplete(error);
		}
	}

	private sealed class SharedTickByTickStream(
		string key,
		ContractDto contract,
		TickByTickType tickType
	)
	{
		public string Key { get; } = key;
		public ContractDto Contract { get; } = contract;
		public TickByTickType TickType { get; } = tickType;
		public List<TickByTickSubscriber> Subscribers { get; } = [];

		// Not disposed: no timer behind it, and the last subscriber may cancel it at any time
		public CancellationTokenSource Cancel { get; } = new();

		// Out of _sharedTickByTick (under _lock)
		public bool IsClosed { get; set; }
	}

	private sealed class TickByTickSubscriber
	{
		// Every tick from the moment it joined, in IB's order; the same (read-only) tick objects
		// go to every subscriber
		public Channel<TickByTickDto> Ticks { get; } =
			Channel.CreateUnbounded<TickByTickDto>(
				new UnboundedChannelOptions { SingleReader = true }
			);
	}

	private sealed class SharedQuoteLine(
		string key,
		long streamId,
		ContractDto contract,
		string genericTicks
	)
	{
		public string Key { get; } = key;
		public long StreamId { get; } = streamId;
		public ContractDto Contract { get; } = contract;
		public string GenericTicks { get; } = genericTicks;
		public List<QuoteSubscriber> Subscribers { get; } = [];

		// Not disposed: no timer behind it, and the last subscriber may cancel it at any time
		public CancellationTokenSource Cancel { get; } = new();

		// Out of the list and of _shared (under _lock)
		public bool IsClosed { get; set; }

		// The cumulative quote so far, handed to whoever joins; null before the first tick
		public QuoteDto? Latest { get; set; }
	}

	private sealed class QuoteSubscriber(string owner)
	{
		public string Owner { get; } = owner;

		// Every quote, in order (as IB's own stream did); the subscriber reads at its pace
		public Channel<QuoteDto> Quotes { get; } =
			Channel.CreateUnbounded<QuoteDto>(new UnboundedChannelOptions { SingleReader = true });
	}
}
