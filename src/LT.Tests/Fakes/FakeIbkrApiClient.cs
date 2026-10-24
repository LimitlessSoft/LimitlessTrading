using System.Runtime.CompilerServices;
using LT.Contracts.Dtos.Account;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Dtos.MarketData;
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

	/// <summary>Answers each search by its query (e.g. per security type); throw to fail one.</summary>
	public Func<ContractDto, IReadOnlyList<ContractDetailsDto>>? SearchBy { get; set; }
	public List<BarDto> HistoricalBars { get; set; } = [];
	public Exception? HistoricalException { get; set; }
	public int HistoricalCalls { get; private set; }
	public List<AccountValueDto> AccountValues { get; set; } = [];
	public List<OptionChainParametersDto> OptionChainParameters { get; set; } = [];
	public List<OpenOrderDto> OpenOrders { get; set; } = [];
	public List<ExecutionDto> Executions { get; set; } = [];

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
		return SearchBy != null
			? Task.Run(() => SearchBy(query), cancellationToken)
			: Task.FromResult<IReadOnlyList<ContractDetailsDto>>(SearchResults);
	}

	public async IAsyncEnumerable<MarketDataTickDto> SubscribeMarketDataAsync(
		ContractDto contract,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
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
	) => Task.FromResult<IReadOnlyList<OptionChainParametersDto>>(OptionChainParameters);

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
	) => AsyncEnumerable.Empty<AccountPnlDto>();

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
	) => Task.FromResult<IReadOnlyList<ExecutionDto>>(Executions);
}
