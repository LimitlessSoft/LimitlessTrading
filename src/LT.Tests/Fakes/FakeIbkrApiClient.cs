using System.Runtime.CompilerServices;
using LT.Contracts.Dtos.Account;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.MarketDepth;
using LT.Contracts.Dtos.News;
using LT.Contracts.Dtos.Options;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Dtos.Positions;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Requests.Connection;

namespace LT.Tests.Fakes;

public class FakeIbkrApiClient : IIbkrApiClient
{
	public bool IsConnected { get; set; }
	public int? OrderClientId { get; set; }
	public string? ManagedAccounts { get; set; } = "DU111,DU222";
	public Exception? ConnectException { get; set; }
	public Exception? ServerTimeException { get; set; }
	public int ConnectCount { get; private set; }
	public int DisconnectCount { get; private set; }
	public MarketDataType? MarketDataType { get; set; }

	public List<PositionDto> Positions { get; set; } = [];
	public List<MarketDataTickDto> Ticks { get; set; } = [];
	public List<ContractDetailsDto> SearchResults { get; set; } = [];
	public ContractDto? LastSearch { get; private set; }
	public int SearchCalls { get; private set; }
	public Exception? SearchException { get; set; }
	public int OptionParameterCalls { get; private set; }
	public Exception? OptionParametersException { get; set; }
	public int ExecutionCalls { get; private set; }
	public Exception? ExecutionsException { get; set; }

	/// <summary>Answers each search by its query (e.g. per security type); throw to fail one.</summary>
	public Func<ContractDto, IReadOnlyList<ContractDetailsDto>>? SearchBy { get; set; }
	public List<BarDto> HistoricalBars { get; set; } = [];
	public Exception? HistoricalException { get; set; }
	public int HistoricalCalls { get; private set; }

	/// <summary>The duration of each historical bars request, in order.</summary>
	public List<string> HistoricalDurations { get; } = [];

	/// <summary>useRTH of each historical bars request, in order.</summary>
	public List<bool> HistoricalUseRth { get; } = [];

	/// <summary>The bar size of each historical bars request, in order.</summary>
	public List<string> HistoricalBarSizes { get; } = [];

	/// <summary>
	/// Answers each historical bars request by duration and bar size instead of
	/// <see cref="HistoricalBars"/>; the answer ignores cancellation, like one already on its way.
	/// </summary>
	public Func<string, string, Task<IReadOnlyList<BarDto>>>? HistoricalBy { get; set; }

	/// <summary>Each historical request's end (null: now).</summary>
	public List<DateTimeOffset?> HistoricalEnds { get; } = [];

	/// <summary>
	/// Answers requests with an end (older pages) instead of <see cref="HistoricalBy"/> /
	/// <see cref="HistoricalBars"/>: given the end, the bars before it.
	/// </summary>
	public Func<DateTimeOffset, Task<IReadOnlyList<BarDto>>>? HistoricalBefore { get; set; }
	public List<AccountValueDto> AccountValues { get; set; } = [];

	/// <summary>Each account's P&amp;L stream; none by default.</summary>
	public Func<
		string,
		CancellationToken,
		IAsyncEnumerable<AccountPnlDto>
	>? AccountPnlSource { get; set; }
	public List<OptionChainParametersDto> OptionChainParameters { get; set; } = [];
	public List<OpenOrderDto> OpenOrders { get; set; } = [];
	public List<ExecutionDto> Executions { get; set; } = [];

	/// <summary>Orders sent through <see cref="PlaceOrderAsync"/>, in order.</summary>
	public List<(ContractDto Contract, OrderDto Order)> PlacedOrders { get; } = [];

	/// <summary>Statuses every placed order streams; the next order id is 100, 101 …</summary>
	public List<OrderStatusDto> OrderStatuses { get; set; } = [];

	/// <summary>When set, the status stream of every placed order instead of <see cref="OrderStatuses"/>.</summary>
	public Func<IAsyncEnumerable<OrderStatusDto>>? OrderStatusSource { get; set; }
	public List<int> CancelledOrderIds { get; } = [];

	public List<NewsProviderDto> NewsProviders { get; set; } = [];

	/// <summary>Past headlines returned for every request (newest first).</summary>
	public List<NewsHeadlineDto> HistoricalNews { get; set; } = [];
	public bool HistoricalNewsHasMore { get; set; }

	/// <summary>Historical news requests: contract, providers, limit, before.</summary>
	public List<(
		int ContractId,
		IReadOnlyList<string> Providers,
		int Max,
		DateTimeOffset? Before
	)> NewsRequests { get; } = [];

	/// <summary>Headlines every news subscription streams; then it stays open until cancelled.</summary>
	public List<NewsHeadlineDto> LiveNews { get; set; } = [];

	/// <summary>
	/// Contracts subscribed to for news, with the providers asked for. Several subscriptions can
	/// start on pool threads at once (merged broad tapes), so read it only after
	/// <see cref="WaitForNewsSubscriptionsAsync"/>.
	/// </summary>
	public List<(
		ContractDto Contract,
		IReadOnlyList<string>? Providers
	)> NewsSubscriptions { get; } = [];

	private readonly List<(int Count, TaskCompletionSource Done)> _newsSubscriptionWaiters = [];

	/// <summary>Provider code → its broad tape contracts; a provider missing here has none.</summary>
	public Dictionary<string, List<ContractDto>> BroadTapes { get; set; } = [];
	public int BroadTapeCalls { get; private set; }
	public NewsArticleDto Article { get; set; } = new() { Body = "Article" };
	public int ArticleCalls { get; private set; }

	public Task ConnectAsync(ConnectRequest request, CancellationToken cancellationToken = default)
	{
		ConnectCount++;
		if (ConnectException != null)
			throw ConnectException;
		IsConnected = true;
		return Task.CompletedTask;
	}

	public Task DisconnectAsync()
	{
		IsConnected = false;
		DisconnectCount++;
		return Task.CompletedTask;
	}

	public Task<DateTimeOffset> GetServerTimeAsync(CancellationToken cancellationToken = default) =>
		ServerTimeException != null
			? Task.FromException<DateTimeOffset>(ServerTimeException)
			: Task.FromResult(new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero));

	public void SetMarketDataType(MarketDataType marketDataType) => MarketDataType = marketDataType;

	public Task<IReadOnlyList<ContractDetailsDto>> SearchContractsAsync(
		ContractDto query,
		CancellationToken cancellationToken = default
	)
	{
		LastSearch = query;
		SearchCalls++;
		if (SearchException != null)
			return Task.FromException<IReadOnlyList<ContractDetailsDto>>(SearchException);
		return SearchBy != null
			? Task.Run(() => SearchBy(query), cancellationToken)
			: Task.FromResult<IReadOnlyList<ContractDetailsDto>>(SearchResults);
	}

	/// <summary>Generic tick lists asked for, in order.</summary>
	public List<string> GenericTicksRequested { get; } = [];

	/// <summary>Contracts of the quote streams asked for, in order.</summary>
	public List<ContractDto> MarketDataRequested { get; } = [];

	/// <summary>Quote streams open now (asked for and not ended or cancelled yet).</summary>
	public int OpenMarketDataStreams => Volatile.Read(ref _openMarketDataStreams);
	private int _openMarketDataStreams;

	public async IAsyncEnumerable<MarketDataTickDto> SubscribeMarketDataAsync(
		ContractDto contract,
		[EnumeratorCancellation] CancellationToken cancellationToken = default,
		string genericTicks = ""
	)
	{
		lock (MarketDataRequested)
		{
			GenericTicksRequested.Add(genericTicks);
			MarketDataRequested.Add(contract);
		}
		Interlocked.Increment(ref _openMarketDataStreams);
		try
		{
			if (MarketDataExceptionFor?.Invoke(contract) is { } refused)
				throw refused;
			foreach (var tick in TicksFor?.Invoke(contract) ?? Ticks)
			{
				await Task.Yield();
				yield return tick;
			}
			if (MarketDataStreamGate is { } gate)
			{
				await foreach (var tick in gate.Reader.ReadAllAsync(cancellationToken))
					yield return tick;
			}
			else if (KeepMarketDataOpen)
				await Task.Delay(Timeout.Infinite, cancellationToken);
		}
		finally
		{
			Interlocked.Decrement(ref _openMarketDataStreams);
		}
	}

	/// <summary>Keep quote streams open after their ticks until cancelled.</summary>
	public bool KeepMarketDataOpen { get; set; }

	/// <summary>Ticks of a contract's stream (instead of <see cref="Ticks"/>).</summary>
	public Func<ContractDto, IReadOnlyList<MarketDataTickDto>>? TicksFor { get; set; }

	/// <summary>Refuses a contract's quote stream with this exception (an IB error), when set.</summary>
	public Func<ContractDto, Exception?>? MarketDataExceptionFor { get; set; }

	/// <summary>
	/// After its ticks, each stream reads this channel: write to push ticks, complete it (with an
	/// exception for an IB error) to end every stream.
	/// </summary>
	public System.Threading.Channels.Channel<MarketDataTickDto>? MarketDataStreamGate { get; set; }

	/// <summary>Contracts of the snapshots asked for, in order.</summary>
	public List<ContractDto> SnapshotsRequested { get; } = [];

	/// <summary>A snapshot's ticks; empty when unset.</summary>
	public Func<ContractDto, IReadOnlyList<MarketDataTickDto>>? SnapshotTicks { get; set; }

	/// <summary>Fails a contract's snapshot with this exception (an IB error), when set.</summary>
	public Func<ContractDto, Exception?>? SnapshotExceptionFor { get; set; }

	/// <summary>Snapshots wait for this before answering (as IB's 11 s); none when null.</summary>
	public TaskCompletionSource? SnapshotGate { get; set; }

	public async Task<IReadOnlyList<MarketDataTickDto>> GetMarketDataSnapshotAsync(
		ContractDto contract,
		CancellationToken cancellationToken = default
	)
	{
		lock (SnapshotsRequested)
			SnapshotsRequested.Add(contract);
		if (SnapshotGate is { } gate)
			await gate.Task.WaitAsync(cancellationToken);
		else
			await Task.Yield();
		if (SnapshotExceptionFor?.Invoke(contract) is { } error)
			throw error;
		return SnapshotTicks?.Invoke(contract) ?? [];
	}

	/// <summary>Updates every depth subscription streams; then it ends, or stays open.</summary>
	public List<MarketDepthUpdateDto> DepthUpdates { get; set; } = [];

	/// <summary>Keep depth streams open after their updates until cancelled.</summary>
	public bool KeepDepthOpen { get; set; }

	/// <summary>Thrown by a depth stream after its updates (an IB refusal).</summary>
	public Exception? DepthException { get; set; }

	public List<(ContractDto Contract, int Rows, bool IsSmartDepth)> DepthRequests { get; } = [];

	public async IAsyncEnumerable<MarketDepthUpdateDto> SubscribeMarketDepthAsync(
		ContractDto contract,
		int rows,
		bool isSmartDepth,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		lock (DepthRequests)
			DepthRequests.Add((contract, rows, isSmartDepth));
		foreach (var update in DepthUpdates)
		{
			await Task.Yield();
			yield return update;
		}
		if (DepthException != null)
			throw DepthException;
		if (KeepDepthOpen)
			await Task.Delay(Timeout.Infinite, cancellationToken);
	}

	public Task<IReadOnlyList<BarDto>> GetHistoricalBarsAsync(
		ContractDto contract,
		string duration,
		string barSize,
		string whatToShow,
		bool useRegularTradingHours,
		DateTimeOffset? end = null,
		CancellationToken cancellationToken = default
	)
	{
		HistoricalCalls++;
		HistoricalDurations.Add(duration);
		HistoricalUseRth.Add(useRegularTradingHours);
		HistoricalBarSizes.Add(barSize);
		HistoricalEnds.Add(end);
		if (end is { } before && HistoricalBefore != null)
			return HistoricalBefore(before);
		if (HistoricalBy != null)
			return HistoricalBy(duration, barSize);
		return HistoricalException != null
			? Task.FromException<IReadOnlyList<BarDto>>(HistoricalException)
			: Task.FromResult<IReadOnlyList<BarDto>>(HistoricalBars);
	}

	public Task<IReadOnlyList<OptionChainParametersDto>> GetOptionChainParametersAsync(
		ContractDto underlying,
		CancellationToken cancellationToken = default
	)
	{
		OptionParameterCalls++;
		return OptionParametersException != null
			? Task.FromException<IReadOnlyList<OptionChainParametersDto>>(OptionParametersException)
			: Task.FromResult<IReadOnlyList<OptionChainParametersDto>>(OptionChainParameters);
	}

	/// <summary>Keep real-time bar streams open (without bars) until cancelled.</summary>
	public bool KeepRealtimeBarsOpen { get; set; }

	public async IAsyncEnumerable<BarDto> SubscribeRealtimeBarsAsync(
		ContractDto contract,
		string whatToShow,
		bool useRegularTradingHours,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		if (!KeepRealtimeBarsOpen)
			yield break;
		await Task.Delay(Timeout.Infinite, cancellationToken);
	}

	/// <summary>Ticks every tick-by-tick subscription streams; then it ends, or throws.</summary>
	public List<TickByTickDto> TickByTicks { get; set; } = [];

	/// <summary>Thrown by a tick-by-tick stream after its ticks (an IB refusal).</summary>
	public Exception? TickByTickException { get; set; }

	/// <summary>Tick-by-tick subscriptions asked for, in order.</summary>
	public List<(ContractDto Contract, TickByTickType Type)> TickByTickRequests { get; } = [];

	/// <summary>Ticks of one tick type's subscriptions instead of <see cref="TickByTicks"/>.</summary>
	public Dictionary<TickByTickType, List<TickByTickDto>> TickByTicksByType { get; } = [];

	/// <summary>Thrown by one tick type's subscriptions after their ticks.</summary>
	public Dictionary<TickByTickType, Exception> TickByTickExceptionByType { get; } = [];

	/// <summary>Past ticks for a page ending at the given time (strictly before it).</summary>
	public Func<DateTimeOffset, IReadOnlyList<TickByTickDto>> HistoricalTicksBefore { get; set; } =
		_ => [];

	/// <summary>Historical ticks requests asked for, in order.</summary>
	public List<(
		ContractDto Contract,
		HistoricalTickType Type,
		int Count,
		DateTimeOffset Before,
		bool UseRegularTradingHours
	)> HistoricalTickRequests { get; } = [];

	public Task<IReadOnlyList<TickByTickDto>> GetHistoricalTicksAsync(
		ContractDto contract,
		HistoricalTickType tickType,
		int count,
		DateTimeOffset before,
		bool useRegularTradingHours,
		CancellationToken cancellationToken = default
	)
	{
		HistoricalTickRequests.Add((contract, tickType, count, before, useRegularTradingHours));
		return Task.FromResult(HistoricalTicksBefore(before));
	}

	public async IAsyncEnumerable<TickByTickDto> SubscribeTickByTickAsync(
		ContractDto contract,
		TickByTickType tickType,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		TickByTickRequests.Add((contract, tickType));
		foreach (var tick in TickByTicksByType.GetValueOrDefault(tickType) ?? TickByTicks)
		{
			await Task.Yield();
			yield return tick;
		}
		if (
			(TickByTickExceptionByType.GetValueOrDefault(tickType) ?? TickByTickException) is
			{ } error
		)
			throw error;
		if (KeepTickByTickOpen)
			await Task.Delay(Timeout.Infinite, cancellationToken);
	}

	/// <summary>Keep tick-by-tick streams open after their ticks until cancelled.</summary>
	public bool KeepTickByTickOpen { get; set; }

	public IAsyncEnumerable<AccountValueDto> SubscribeAccountSummaryAsync(
		CancellationToken cancellationToken = default
	) => AccountValues.ToAsyncEnumerable();

	public IAsyncEnumerable<AccountPnlDto> SubscribeAccountPnlAsync(
		string account,
		CancellationToken cancellationToken = default
	) =>
		AccountPnlSource?.Invoke(account, cancellationToken)
		?? AsyncEnumerable.Empty<AccountPnlDto>();

	public Task<IReadOnlyList<PositionDto>> GetPositionsAsync(
		CancellationToken cancellationToken = default
	) => Task.FromResult<IReadOnlyList<PositionDto>>(Positions);

	public IAsyncEnumerable<PositionDto> SubscribePositionsAsync(
		CancellationToken cancellationToken = default
	) => Positions.ToAsyncEnumerable();

	public IAsyncEnumerable<PositionPnlDto> SubscribePositionPnlAsync(
		string account,
		int contractId,
		CancellationToken cancellationToken = default
	) => AsyncEnumerable.Empty<PositionPnlDto>();

	public Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(
		CancellationToken cancellationToken = default
	) => Task.FromResult<IReadOnlyList<OpenOrderDto>>(OpenOrders);

	public Task<IReadOnlyList<ExecutionDto>> GetExecutionsAsync(
		CancellationToken cancellationToken = default
	)
	{
		ExecutionCalls++;
		return ExecutionsException != null
			? Task.FromException<IReadOnlyList<ExecutionDto>>(ExecutionsException)
			: Task.FromResult<IReadOnlyList<ExecutionDto>>(Executions);
	}

	public Task<PlacedOrderDto> PlaceOrderAsync(
		ContractDto contract,
		OrderDto order,
		CancellationToken cancellationToken = default
	)
	{
		PlacedOrders.Add((contract, order));
		return Task.FromResult(
			new PlacedOrderDto
			{
				OrderId = 99 + PlacedOrders.Count,
				Statuses = OrderStatusSource?.Invoke() ?? OrderStatuses.ToAsyncEnumerable(),
			}
		);
	}

	/// <summary>The WSH metadata JSON; thrown instead when <see cref="WshException"/> is set.</summary>
	public string WshMetaData { get; set; } = "{}";

	/// <summary>Thrown by every WSH request (an IB refusal such as 10276).</summary>
	public Exception? WshException { get; set; }

	/// <summary>Answers each event data request; the default is an empty list.</summary>
	public Func<WshEventQueryDto, string> WshEventsBy { get; set; } = _ => "[]";

	public int WshMetaDataCalls { get; private set; }

	/// <summary>Every event data request, in order.</summary>
	public List<WshEventQueryDto> WshEventRequests { get; } = [];

	public Task<string> GetWshMetaDataAsync(CancellationToken cancellationToken = default)
	{
		WshMetaDataCalls++;
		return WshException != null
			? Task.FromException<string>(WshException)
			: Task.FromResult(WshMetaData);
	}

	public Task<string> GetWshEventDataAsync(
		WshEventQueryDto query,
		CancellationToken cancellationToken = default
	)
	{
		WshEventRequests.Add(query);
		return WshException != null
			? Task.FromException<string>(WshException)
			: Task.FromResult(WshEventsBy(query));
	}

	public Task CancelOrderAsync(int orderId, CancellationToken cancellationToken = default)
	{
		CancelledOrderIds.Add(orderId);
		return Task.CompletedTask;
	}

	public Task<IReadOnlyList<NewsProviderDto>> GetNewsProvidersAsync(
		CancellationToken cancellationToken = default
	) =>
		Task.FromResult<IReadOnlyList<NewsProviderDto>>([
			.. NewsProviders.Select(x => new NewsProviderDto { Code = x.Code, Name = x.Name }),
		]);

	public Task<NewsHeadlinesPageDto> GetHistoricalNewsAsync(
		int contractId,
		IReadOnlyList<string> providerCodes,
		int maxResults,
		DateTimeOffset? before = null,
		CancellationToken cancellationToken = default
	)
	{
		NewsRequests.Add((contractId, providerCodes, maxResults, before));
		return Task.FromResult(
			new NewsHeadlinesPageDto
			{
				Headlines =
				[
					.. HistoricalNews
						.Where(x => before == null || x.Time < before)
						.Take(maxResults)
						.Select(Copy),
				],
				HasMore = HistoricalNewsHasMore,
			}
		);
	}

	public Task<NewsArticleDto> GetNewsArticleAsync(
		string providerCode,
		string articleId,
		CancellationToken cancellationToken = default
	)
	{
		ArticleCalls++;
		return Task.FromResult(Article);
	}

	public async IAsyncEnumerable<NewsHeadlineDto> SubscribeNewsAsync(
		ContractDto contract,
		IReadOnlyList<string>? providerCodes = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		lock (NewsSubscriptions)
		{
			NewsSubscriptions.Add((contract, providerCodes));
			foreach (var waiter in _newsSubscriptionWaiters)
				if (NewsSubscriptions.Count >= waiter.Count)
					waiter.Done.TrySetResult();
		}
		foreach (var headline in LiveNews)
		{
			await Task.Yield();
			yield return Copy(headline);
		}
		await Task.Delay(Timeout.Infinite, cancellationToken);
	}

	/// <summary>Completes once <paramref name="count"/> news subscriptions have started.</summary>
	public Task WaitForNewsSubscriptionsAsync(int count, CancellationToken cancellationToken)
	{
		var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		lock (NewsSubscriptions)
		{
			if (NewsSubscriptions.Count >= count)
				return Task.CompletedTask;
			_newsSubscriptionWaiters.Add((count, done));
		}
		return done.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
	}

	public Task<IReadOnlyList<ContractDto>> GetBroadTapeContractsAsync(
		string providerCode,
		CancellationToken cancellationToken = default
	)
	{
		BroadTapeCalls++;
		return Task.FromResult<IReadOnlyList<ContractDto>>(
			BroadTapes.TryGetValue(providerCode, out var tapes) ? tapes : []
		);
	}

	// Callers fill in source and contract; every call gets its own copy, as from IB
	private static NewsHeadlineDto Copy(NewsHeadlineDto x) =>
		new()
		{
			ProviderCode = x.ProviderCode,
			ArticleId = x.ArticleId,
			Time = x.Time,
			Headline = x.Headline,
			Language = x.Language,
			Sentiment = x.Sentiment,
			Confidence = x.Confidence,
		};
}
