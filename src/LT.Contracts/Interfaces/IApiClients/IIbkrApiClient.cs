using LT.Contracts.Dtos.Account;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Dtos.Positions;
using LT.Contracts.Enums;
using LT.Contracts.Requests.Connection;

namespace LT.Contracts.Interfaces.IApiClients;

/// <summary>
/// Boundary to Interactive Brokers (TWS / IB Gateway socket API). Implemented in LT.Ibkr; the only
/// place where TwsApi / IBApi types exist. Everything crossing this interface is an LT DTO.
/// Read-only: there are deliberately no order placement methods.
/// </summary>
public interface IIbkrApiClient
{
	bool IsConnected { get; }
	string? ManagedAccounts { get; }
	Task ConnectAsync(ConnectRequest request, CancellationToken cancellationToken = default);
	Task DisconnectAsync();
	Task<DateTimeOffset> GetServerTimeAsync(CancellationToken cancellationToken = default);
	void SetMarketDataType(MarketDataType marketDataType);

	Task<IReadOnlyList<ContractDetailsDto>> SearchContractsAsync(
		ContractDto query,
		CancellationToken cancellationToken = default
	);

	IAsyncEnumerable<MarketDataTickDto> SubscribeMarketDataAsync(
		ContractDto contract,
		CancellationToken cancellationToken = default
	);

	Task<IReadOnlyList<BarDto>> GetHistoricalBarsAsync(
		ContractDto contract,
		string duration,
		string barSize,
		string whatToShow,
		bool useRegularTradingHours,
		CancellationToken cancellationToken = default
	);

	IAsyncEnumerable<BarDto> SubscribeRealtimeBarsAsync(
		ContractDto contract,
		string whatToShow,
		bool useRegularTradingHours,
		CancellationToken cancellationToken = default
	);

	Task<IReadOnlyList<AccountValueDto>> GetAccountSummaryAsync(
		IReadOnlyList<string> tags,
		CancellationToken cancellationToken = default
	);

	IAsyncEnumerable<AccountPnlDto> SubscribeAccountPnlAsync(
		string account,
		CancellationToken cancellationToken = default
	);

	Task<IReadOnlyList<PositionDto>> GetPositionsAsync(
		CancellationToken cancellationToken = default
	);

	IAsyncEnumerable<PositionDto> SubscribePositionsAsync(
		CancellationToken cancellationToken = default
	);

	IAsyncEnumerable<PositionPnlDto> SubscribePositionPnlAsync(
		string account,
		int contractId,
		CancellationToken cancellationToken = default
	);

	Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(
		CancellationToken cancellationToken = default
	);

	Task<IReadOnlyList<ExecutionDto>> GetExecutionsAsync(
		CancellationToken cancellationToken = default
	);
}
