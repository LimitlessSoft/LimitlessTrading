using System.Runtime.CompilerServices;
using IBApi;
using LSCore.Mapper.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Account;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Dtos.Positions;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Requests.Connection;
using TwsApi;

namespace LT.Ibkr;

/// <summary>
/// IIbkrApiClient over TwsApi. Holds the one live ITwsClient and paces every request so the app
/// stays inside IB's limits. Registered as a singleton.
/// </summary>
public class IbkrApiClient(ITwsClientFactory twsClientFactory) : IIbkrApiClient
{
	private readonly RequestThrottle _messages = new(
		MarketDataConstants.MaxMessagesPerSecond,
		TimeSpan.FromSeconds(1)
	);
	private readonly RequestThrottle _historical = new(
		MarketDataConstants.MaxHistoricalRequests,
		MarketDataConstants.HistoricalWindow
	);

	private ITwsClient? _client;
	private AccountSummaryFeed? _accountSummary;

	public bool IsConnected => _client?.IsConnected == true;

	public string? ManagedAccounts => _client?.ManagedAccounts;

	public async Task ConnectAsync(
		ConnectRequest request,
		CancellationToken cancellationToken = default
	)
	{
		await DisconnectAsync();
		_client = await twsClientFactory.ConnectAsync(
			new TwsConnectionOptions
			{
				Host = request.Host,
				Port = request.Port,
				ClientId = request.ClientId,
				ConnectTimeout = ConnectionConstants.ConnectTimeout,
			},
			cancellationToken
		);
		_accountSummary = new AccountSummaryFeed(_client, _messages);
	}

	public async Task DisconnectAsync()
	{
		var accountSummary = Interlocked.Exchange(ref _accountSummary, null);
		if (accountSummary != null)
			await accountSummary.DisposeAsync();

		var client = Interlocked.Exchange(ref _client, null);
		if (client != null)
			await client.DisposeAsync();
	}

	public async Task<DateTimeOffset> GetServerTimeAsync(
		CancellationToken cancellationToken = default
	)
	{
		await _messages.WaitAsync(cancellationToken);
		return await RequireClient().GetServerTimeAsync(cancellationToken);
	}

	public void SetMarketDataType(MarketDataType marketDataType) =>
		_client?.SetMarketDataType((int)marketDataType);

	public async Task<IReadOnlyList<ContractDetailsDto>> SearchContractsAsync(
		ContractDto query,
		CancellationToken cancellationToken = default
	)
	{
		await _messages.WaitAsync(cancellationToken);
		var details = await RequireClient()
			.ResolveContractAsync(query.ToMapped<ContractDto, Contract>(), cancellationToken);
		return details.ToMappedList<ContractDetails, ContractDetailsDto>();
	}

	public IAsyncEnumerable<MarketDataTickDto> SubscribeMarketDataAsync(
		ContractDto contract,
		CancellationToken cancellationToken = default
	) =>
		StreamAsync<MarketDataTick, MarketDataTickDto>(
			(client, ct) =>
				client.SubscribeMarketDataAsync(
					contract.ToMapped<ContractDto, Contract>(),
					cancellationToken: ct
				),
			cancellationToken
		);

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
				useRegularTradingHours: useRegularTradingHours,
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
		StreamAsync<RealtimeBar, BarDto>(
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
		StreamAsync<AccountPnl, AccountPnlDto>(
			(client, ct) => client.SubscribeAccountPnlAsync(account, cancellationToken: ct),
			cancellationToken
		);

	public async Task<IReadOnlyList<PositionDto>> GetPositionsAsync(
		CancellationToken cancellationToken = default
	)
	{
		await _messages.WaitAsync(cancellationToken);
		var positions = await RequireClient().GetPositionsAsync(cancellationToken);
		return positions.ToMappedList<PositionInfo, PositionDto>();
	}

	public IAsyncEnumerable<PositionDto> SubscribePositionsAsync(
		CancellationToken cancellationToken = default
	) =>
		StreamAsync<PositionInfo, PositionDto>(
			(client, ct) => client.SubscribePositionsAsync(ct),
			cancellationToken
		);

	public IAsyncEnumerable<PositionPnlDto> SubscribePositionPnlAsync(
		string account,
		int contractId,
		CancellationToken cancellationToken = default
	) =>
		StreamAsync<PositionPnl, PositionPnlDto>(
			(client, ct) =>
				client.SubscribePositionPnlAsync(account, contractId, cancellationToken: ct),
			cancellationToken
		);

	public async Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(
		CancellationToken cancellationToken = default
	)
	{
		await _messages.WaitAsync(cancellationToken);
		var orders = await RequireClient().GetOpenOrdersAsync(cancellationToken);
		return orders.ToMappedList<OpenOrderInfo, OpenOrderDto>();
	}

	public async Task<IReadOnlyList<ExecutionDto>> GetExecutionsAsync(
		CancellationToken cancellationToken = default
	)
	{
		await _messages.WaitAsync(cancellationToken);
		var executions = await RequireClient()
			.GetExecutionsAsync(cancellationToken: cancellationToken);
		return executions.ToMappedList<ExecutionInfo, ExecutionDto>();
	}

	private ITwsClient RequireClient() =>
		_client ?? throw new InvalidOperationException("Not connected to TWS / IB Gateway.");

	// TwsApi sends the subscribe request when enumeration starts, so pacing happens right before it
	private async IAsyncEnumerable<TDto> StreamAsync<TSource, TDto>(
		Func<ITwsClient, CancellationToken, IAsyncEnumerable<TSource>> subscribe,
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
