using System.Runtime.CompilerServices;
using IBKR.Net;
using LSCore.Mapper.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Account;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.Options;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Dtos.Positions;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Requests.Connection;
using Contract = IBKR.Net.Contract;
using ExecutionInfo = TwsApi.ExecutionInfo;
using ITwsClient = TwsApi.ITwsClient;
using ITwsClientFactory = TwsApi.ITwsClientFactory;
using MarketDataType = LT.Contracts.Enums.MarketDataType;
using OpenOrderInfo = TwsApi.OpenOrderInfo;
using RealtimeBar = TwsApi.RealtimeBar;
using TwsConnectionOptions = TwsApi.TwsConnectionOptions;
using TwsContract = IBApi.Contract;

namespace LT.Ibkr;

/// <summary>
/// IIbkrApiClient over our own IBKR.Net, plus a second connection through TwsApi for the three
/// calls IBKR.Net does not cover yet (real-time bars, open orders, executions; see
/// docs/ibkr-tws-api.md). Paces every request so the app stays inside IB's limits. Registered as
/// a singleton.
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

	private IbkrClient? _client;
	private ITwsClient? _tws;
	private AccountSummaryFeed? _accountSummary;

	public bool IsConnected => _client?.IsConnected == true && _tws?.IsConnected == true;

	public string? ManagedAccounts =>
		_client is { } client ? string.Join(",", client.ManagedAccounts) : null;

	public async Task ConnectAsync(
		ConnectRequest request,
		CancellationToken cancellationToken = default
	)
	{
		await DisconnectAsync();
		var client = await IbkrClient.ConnectAsync(
			new IbkrClientOptions
			{
				Host = request.Host,
				Port = request.Port,
				ClientId = request.ClientId,
				ConnectTimeout = ConnectionConstants.ConnectTimeout,
			},
			cancellationToken
		);
		try
		{
			_tws = await twsClientFactory.ConnectAsync(
				new TwsConnectionOptions
				{
					Host = request.Host,
					Port = request.Port,
					ClientId = request.ClientId + ConnectionConstants.TwsApiClientIdOffset,
					ConnectTimeout = ConnectionConstants.ConnectTimeout,
				},
				cancellationToken
			);
		}
		catch
		{
			await client.DisposeAsync();
			throw;
		}
		_client = client;
		_accountSummary = new AccountSummaryFeed(ct =>
			StreamAsync<AccountValue, AccountValueDto>(
				(c, token) =>
					c.SubscribeAccountSummaryAsync(
						AccountConstants.SummaryTags,
						cancellationToken: token
					),
				ct
			)
		);
	}

	public async Task DisconnectAsync()
	{
		var accountSummary = Interlocked.Exchange(ref _accountSummary, null);
		if (accountSummary != null)
			await accountSummary.DisposeAsync();

		var client = Interlocked.Exchange(ref _client, null);
		if (client != null)
			await client.DisposeAsync();

		var tws = Interlocked.Exchange(ref _tws, null);
		if (tws != null)
			await tws.DisposeAsync();
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
				useRegularTradingHours,
				cancellationToken: cancellationToken
			);
		return bars.ToMappedList<Bar, BarDto>();
	}

	public async IAsyncEnumerable<BarDto> SubscribeRealtimeBarsAsync(
		ContractDto contract,
		string whatToShow,
		bool useRegularTradingHours,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var tws = RequireTws();
		await _messages.WaitAsync(cancellationToken);
		await foreach (
			var bar in tws.SubscribeRealtimeBarsAsync(
				contract.ToMapped<ContractDto, TwsContract>(),
				whatToShow,
				useRegularTradingHours,
				cancellationToken
			)
		)
			yield return bar.ToMapped<RealtimeBar, BarDto>();
	}

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
			(client, ct) => client.SubscribeAccountPnlAsync(account, ct),
			cancellationToken
		);

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

	public async Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(
		CancellationToken cancellationToken = default
	)
	{
		await _messages.WaitAsync(cancellationToken);
		var orders = await RequireTws().GetOpenOrdersAsync(cancellationToken);
		return orders.ToMappedList<OpenOrderInfo, OpenOrderDto>();
	}

	public async Task<IReadOnlyList<ExecutionDto>> GetExecutionsAsync(
		CancellationToken cancellationToken = default
	)
	{
		await _messages.WaitAsync(cancellationToken);
		var executions = await RequireTws()
			.GetExecutionsAsync(cancellationToken: cancellationToken);
		return executions.ToMappedList<ExecutionInfo, ExecutionDto>();
	}

	private IbkrClient RequireClient() =>
		_client ?? throw new InvalidOperationException("Not connected to TWS / IB Gateway.");

	private ITwsClient RequireTws() =>
		_tws ?? throw new InvalidOperationException("Not connected to TWS / IB Gateway.");

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
