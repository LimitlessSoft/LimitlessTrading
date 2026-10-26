using System.Runtime.CompilerServices;
using IBKR.Net;
using LSCore.Mapper.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Account;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.MarketDepth;
using LT.Contracts.Dtos.News;
using LT.Contracts.Dtos.Options;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Dtos.Positions;
using LT.Contracts.Exceptions;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Requests.Connection;
using Contract = IBKR.Net.Contract;
using MarketDataType = LT.Contracts.Enums.MarketDataType;
using Order = IBKR.Net.Order;

namespace LT.Ibkr;

/// <summary>
/// IIbkrApiClient over our own IBKR.Net (no Interactive Brokers code; see docs/ibkr-tws-api.md).
/// Holds the live connection (plus one for orders, opened on the first order) and paces every
/// request so the app stays inside IB's limits. Registered as a singleton.
/// </summary>
public class IbkrApiClient : IIbkrApiClient
{
	private readonly RequestThrottle _messages = new(
		MarketDataConstants.MaxMessagesPerSecond,
		TimeSpan.FromSeconds(1)
	);
	private readonly RequestThrottle _historical = new(
		MarketDataConstants.MaxHistoricalRequests,
		MarketDataConstants.HistoricalWindow
	);

	private readonly SemaphoreSlim _orderConnect = new(1, 1);

	private IbkrClient? _client;
	private AccountSummaryFeed? _accountSummary;
	private AccountPnlFeed? _accountPnl;
	private ConnectRequest? _endpoint;

	// IBKR.Net places orders only at the protocol version they were observed at (178)
	private IbkrClient? _orderClient;

	public bool IsConnected => _client?.IsConnected == true;

	public int? OrderClientId =>
		IsConnected && _endpoint is { } endpoint
			? OrderHelpers.GetOrderClientId(endpoint.ClientId)
			: null;

	public string? ManagedAccounts =>
		_client is { } client ? string.Join(",", client.ManagedAccounts) : null;

	public async Task ConnectAsync(
		ConnectRequest request,
		CancellationToken cancellationToken = default
	)
	{
		await DisconnectAsync();
		_endpoint = request;
		_client = await IbkrClient.ConnectAsync(
			new IbkrClientOptions
			{
				Host = request.Host,
				Port = request.Port,
				ClientId = request.ClientId,
				ConnectTimeout = ConnectionConstants.ConnectTimeout,
			},
			cancellationToken
		);
		_accountSummary = new AccountSummaryFeed(ct =>
			StreamAsync<AccountValue, AccountValueDto>(
				(client, token) =>
					client.SubscribeAccountSummaryAsync(
						AccountConstants.SummaryTags,
						cancellationToken: token
					),
				ct
			)
		);
		_accountPnl = new AccountPnlFeed(
			(account, ct) =>
				StreamAsync<AccountPnl, AccountPnlDto>(
					(client, token) => client.SubscribeAccountPnlAsync(account, token),
					ct
				)
		);
	}

	public async Task DisconnectAsync()
	{
		var accountSummary = Interlocked.Exchange(ref _accountSummary, null);
		if (accountSummary != null)
			await accountSummary.DisposeAsync();

		var accountPnl = Interlocked.Exchange(ref _accountPnl, null);
		if (accountPnl != null)
			await accountPnl.DisposeAsync();

		var client = Interlocked.Exchange(ref _client, null);
		if (client != null)
			await client.DisposeAsync();

		var orderClient = Interlocked.Exchange(ref _orderClient, null);
		if (orderClient != null)
			await orderClient.DisposeAsync();
	}

	public async Task<DateTimeOffset> GetServerTimeAsync(
		CancellationToken cancellationToken = default
	)
	{
		await _messages.WaitAsync(cancellationToken);
		return await RequireClient().GetServerTimeAsync(cancellationToken);
	}

	public void SetMarketDataType(MarketDataType marketDataType)
	{
		if (_client is { } client)
			_ = SendQuietlyAsync(() =>
				client.SetMarketDataTypeAsync((IBKR.Net.MarketDataType)marketDataType)
			);
	}

	public async Task<IReadOnlyList<ContractDetailsDto>> SearchContractsAsync(
		ContractDto query,
		CancellationToken cancellationToken = default
	)
	{
		await _messages.WaitAsync(cancellationToken);
		var details = await RequireClient()
			.GetContractDetailsAsync(query.ToMapped<ContractDto, Contract>(), cancellationToken);
		return details.ToMappedList<ContractDetails, ContractDetailsDto>();
	}

	public async Task<IReadOnlyList<OptionChainParametersDto>> GetOptionChainParametersAsync(
		ContractDto underlying,
		CancellationToken cancellationToken = default
	)
	{
		await _messages.WaitAsync(cancellationToken);
		var parameters = await RequireClient()
			.GetOptionChainParametersAsync(
				underlying.Symbol,
				underlying.SecurityType,
				underlying.ConId,
				cancellationToken: cancellationToken
			);
		return parameters.ToMappedList<OptionChainParameters, OptionChainParametersDto>();
	}

	public IAsyncEnumerable<MarketDataTickDto> SubscribeMarketDataAsync(
		ContractDto contract,
		CancellationToken cancellationToken = default,
		string genericTicks = ""
	) =>
		StreamAsync<MarketDataTick, MarketDataTickDto>(
			(client, ct) =>
				client.SubscribeMarketDataAsync(
					contract.ToMapped<ContractDto, Contract>(),
					genericTicks,
					cancellationToken: ct
				),
			cancellationToken
		);

	// IB's refusals (309 too many, 354 / 10089 no subscription) go out with their code
	public async IAsyncEnumerable<MarketDepthUpdateDto> SubscribeMarketDepthAsync(
		ContractDto contract,
		int rows,
		bool isSmartDepth,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		await using var updates = StreamAsync<MarketDepthUpdate, MarketDepthUpdateDto>(
				(client, ct) =>
					client.SubscribeMarketDepthAsync(
						contract.ToMapped<ContractDto, Contract>(),
						rows,
						isSmartDepth,
						ct
					),
				cancellationToken
			)
			.GetAsyncEnumerator(cancellationToken);
		while (true)
		{
			bool more;
			try
			{
				more = await updates.MoveNextAsync();
			}
			catch (IbkrException ex)
			{
				throw new IbkrRequestException(ex.Code, ex.ErrorText, ex);
			}
			if (!more)
				break;
			yield return updates.Current;
		}
	}

	public async Task<IReadOnlyList<BarDto>> GetHistoricalBarsAsync(
		ContractDto contract,
		string duration,
		string barSize,
		string whatToShow,
		bool useRegularTradingHours,
		CancellationToken cancellationToken = default
	)
	{
		await _historical.WaitAsync(cancellationToken);
		await _messages.WaitAsync(cancellationToken);
		var bars = await RequireClient()
			.GetHistoricalBarsAsync(
				contract.ToMapped<ContractDto, Contract>(),
				duration,
				barSize,
				whatToShow,
				useRegularTradingHours,
				cancellationToken: cancellationToken
			);
		return bars.ToMappedList<Bar, BarDto>();
	}

	public IAsyncEnumerable<BarDto> SubscribeRealtimeBarsAsync(
		ContractDto contract,
		string whatToShow,
		bool useRegularTradingHours,
		CancellationToken cancellationToken = default
	) =>
		StreamAsync<Bar, BarDto>(
			(client, ct) =>
				client.SubscribeRealtimeBarsAsync(
					contract.ToMapped<ContractDto, Contract>(),
					whatToShow,
					useRegularTradingHours,
					ct
				),
			cancellationToken
		);

	public IAsyncEnumerable<AccountValueDto> SubscribeAccountSummaryAsync(
		CancellationToken cancellationToken = default
	) =>
		(
			_accountSummary
			?? throw new InvalidOperationException("Not connected to TWS / IB Gateway.")
		).SubscribeAsync(cancellationToken);

	public IAsyncEnumerable<AccountPnlDto> SubscribeAccountPnlAsync(
		string account,
		CancellationToken cancellationToken = default
	) =>
		(
			_accountPnl ?? throw new InvalidOperationException("Not connected to TWS / IB Gateway.")
		).SubscribeAsync(account, cancellationToken);

	public async Task<IReadOnlyList<PositionDto>> GetPositionsAsync(
		CancellationToken cancellationToken = default
	)
	{
		await _messages.WaitAsync(cancellationToken);
		var positions = await RequireClient().GetPositionsAsync(cancellationToken);
		return positions.ToMappedList<Position, PositionDto>();
	}

	public IAsyncEnumerable<PositionDto> SubscribePositionsAsync(
		CancellationToken cancellationToken = default
	) =>
		StreamAsync<Position, PositionDto>(
			(client, ct) => client.SubscribePositionsAsync(ct),
			cancellationToken
		);

	public IAsyncEnumerable<PositionPnlDto> SubscribePositionPnlAsync(
		string account,
		int contractId,
		CancellationToken cancellationToken = default
	) =>
		StreamAsync<PositionPnl, PositionPnlDto>(
			(client, ct) => client.SubscribePositionPnlAsync(account, contractId, ct),
			cancellationToken
		);

	public async Task<IReadOnlyList<NewsProviderDto>> GetNewsProvidersAsync(
		CancellationToken cancellationToken = default
	)
	{
		await _messages.WaitAsync(cancellationToken);
		var providers = await RequireClient().GetNewsProvidersAsync(cancellationToken);
		return providers.ToMappedList<NewsProvider, NewsProviderDto>();
	}

	public async Task<NewsHeadlinesPageDto> GetHistoricalNewsAsync(
		int contractId,
		IReadOnlyList<string> providerCodes,
		int maxResults,
		DateTimeOffset? before = null,
		CancellationToken cancellationToken = default
	)
	{
		await _messages.WaitAsync(cancellationToken);
		var news = await RequireClient()
			.GetHistoricalNewsAsync(
				contractId,
				providerCodes,
				maxResults,
				before,
				cancellationToken
			);
		return new NewsHeadlinesPageDto
		{
			Headlines = news.Headlines.ToMappedList<NewsHeadline, NewsHeadlineDto>(),
			HasMore = news.HasMore,
		};
	}

	public async Task<NewsArticleDto> GetNewsArticleAsync(
		string providerCode,
		string articleId,
		CancellationToken cancellationToken = default
	)
	{
		await _messages.WaitAsync(cancellationToken);
		var article = await RequireClient()
			.GetNewsArticleAsync(providerCode, articleId, cancellationToken);
		return article.ToMapped<NewsArticle, NewsArticleDto>();
	}

	public IAsyncEnumerable<NewsHeadlineDto> SubscribeNewsAsync(
		ContractDto contract,
		IReadOnlyList<string>? providerCodes = null,
		CancellationToken cancellationToken = default
	) =>
		StreamAsync<NewsHeadline, NewsHeadlineDto>(
			(client, ct) =>
				client.SubscribeNewsAsync(
					contract.ToMapped<ContractDto, Contract>(),
					providerCodes,
					ct
				),
			cancellationToken
		);

	// IB answers 321 (invalid news source) or 200 (no definition) for a provider without one
	public async Task<IReadOnlyList<ContractDto>> GetBroadTapeContractsAsync(
		string providerCode,
		CancellationToken cancellationToken = default
	)
	{
		await _messages.WaitAsync(cancellationToken);
		try
		{
			var contracts = await RequireClient()
				.GetBroadTapeContractsAsync(providerCode, cancellationToken);
			return contracts.ToMappedList<Contract, ContractDto>();
		}
		catch (IbkrException ex) when (NewsConstants.NoBroadTapeErrorCodes.Contains(ex.Code))
		{
			return [];
		}
	}

	// Every client's orders: the ones placed here go out on the order connection's client id
	public async Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(
		CancellationToken cancellationToken = default
	)
	{
		await _messages.WaitAsync(cancellationToken);
		var orders = await RequireClient().GetOpenOrdersAsync(cancellationToken);
		return orders.ToMappedList<OpenOrder, OpenOrderDto>();
	}

	// The day's fills of every account and client, each with its commission report
	public async Task<IReadOnlyList<ExecutionDto>> GetExecutionsAsync(
		CancellationToken cancellationToken = default
	)
	{
		await _messages.WaitAsync(cancellationToken);
		var executions = await RequireClient().GetExecutionsAsync(cancellationToken);
		return executions.ToMappedList<Execution, ExecutionDto>();
	}

	public async Task<PlacedOrderDto> PlaceOrderAsync(
		ContractDto contract,
		OrderDto order,
		CancellationToken cancellationToken = default
	)
	{
		var client = await RequireOrderClientAsync(cancellationToken);
		await _messages.WaitAsync(cancellationToken);
		var placed = await client.PlaceOrderAsync(
			contract.ToMapped<ContractDto, Contract>(),
			order.ToMapped<OrderDto, Order>(),
			cancellationToken
		);
		return new PlacedOrderDto
		{
			OrderId = placed.OrderId,
			Statuses = MapStatusesAsync(placed.Statuses),
		};
	}

	public async Task CancelOrderAsync(int orderId, CancellationToken cancellationToken = default)
	{
		var client = await RequireOrderClientAsync(cancellationToken);
		await _messages.WaitAsync(cancellationToken);
		await client.CancelOrderAsync(orderId, cancellationToken);
	}

	// The order connection: opened on the first order, again after it dropped
	private async Task<IbkrClient> RequireOrderClientAsync(CancellationToken cancellationToken)
	{
		var endpoint =
			_client != null && _endpoint is { } connected
				? connected
				: throw new InvalidOperationException("Not connected to TWS / IB Gateway.");
		await _orderConnect.WaitAsync(cancellationToken);
		try
		{
			if (_orderClient is { IsConnected: true } open)
				return open;
			if (_orderClient is { } closed)
				await closed.DisposeAsync();
			_orderClient = await IbkrClient.ConnectAsync(
				new IbkrClientOptions
				{
					Host = endpoint.Host,
					Port = endpoint.Port,
					ClientId = OrderHelpers.GetOrderClientId(endpoint.ClientId),
					ConnectTimeout = ConnectionConstants.ConnectTimeout,
					MaxServerVersion = IbkrClient.OrderProtocolVersion,
				},
				cancellationToken
			);
			return _orderClient;
		}
		finally
		{
			_orderConnect.Release();
		}
	}

	private static async IAsyncEnumerable<OrderStatusDto> MapStatusesAsync(
		IAsyncEnumerable<OrderStatus> statuses,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		await foreach (var status in statuses.WithCancellation(cancellationToken))
			yield return status.ToMapped<OrderStatus, OrderStatusDto>();
	}

	private IbkrClient RequireClient() =>
		_client ?? throw new InvalidOperationException("Not connected to TWS / IB Gateway.");

	private static async Task SendQuietlyAsync(Func<Task> send)
	{
		try
		{
			await send();
		}
		catch (IOException) { }
	}

	// The subscribe request goes out when enumeration starts, so pacing happens right before it
	private async IAsyncEnumerable<TDto> StreamAsync<TSource, TDto>(
		Func<IbkrClient, CancellationToken, IAsyncEnumerable<TSource>> subscribe,
		[EnumeratorCancellation] CancellationToken cancellationToken
	)
		where TSource : class
		where TDto : class
	{
		var client = RequireClient();
		await _messages.WaitAsync(cancellationToken);
		await foreach (var item in subscribe(client, cancellationToken))
			yield return item.ToMapped<TSource, TDto>();
	}
}
