using System.Runtime.CompilerServices;
using LT.Contracts.Dtos.Account;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Dtos.MarketData;
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
	public MarketDataType? MarketDataType { get; private set; }

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

	public async IAsyncEnumerable<MarketDataTickDto> SubscribeMarketDataAsync(
		ContractDto contract,
		[EnumeratorCancellation] CancellationToken cancellationToken = default,
		string genericTicks = ""
	)
	{
		GenericTicksRequested.Add(genericTicks);
		foreach (var tick in Ticks)
		{
			await Task.Yield();
			yield return tick;
		}
	}

	public Task<IReadOnlyList<BarDto>> GetHistoricalBarsAsync(
		ContractDto contract,
		string duration,
		string barSize,
		string whatToShow,
		bool useRegularTradingHours,
		CancellationToken cancellationToken = default
	)
	{
		HistoricalCalls++;
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

	public IAsyncEnumerable<BarDto> SubscribeRealtimeBarsAsync(
		ContractDto contract,
		string whatToShow,
		bool useRegularTradingHours,
		CancellationToken cancellationToken = default
	) => AsyncEnumerable.Empty<BarDto>();

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
				Statuses = OrderStatuses.ToAsyncEnumerable(),
			}
		);
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
