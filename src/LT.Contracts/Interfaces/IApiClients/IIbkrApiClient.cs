using LT.Contracts.Dtos.Account;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.Options;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Dtos.Positions;
using LT.Contracts.Enums;
using LT.Contracts.Requests.Connection;

namespace LT.Contracts.Interfaces.IApiClients;

/// <summary>
/// Boundary to Interactive Brokers (TWS / IB Gateway socket API). Implemented in LT.Ibkr; the only
/// place where IBKR.Net types exist. Everything crossing this interface is an LT DTO.
/// Orders go over a second connection of their own (see <see cref="PlaceOrderAsync"/>).
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

	/// <summary>Expirations and strikes of the underlying's options (reqSecDefOptParams).</summary>
	Task<IReadOnlyList<OptionChainParametersDto>> GetOptionChainParametersAsync(
		ContractDto underlying,
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

	/// <summary>
	/// Account summary values (<c>AccountConstants.SummaryTags</c>) of every managed account: the
	/// latest values first, then each change. All callers share one IB subscription per connection.
	/// </summary>
	IAsyncEnumerable<AccountValueDto> SubscribeAccountSummaryAsync(
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

	/// <summary>
	/// Sends an order. This trades for real on a live account. Uses a second connection at the
	/// protocol version orders were observed at, opened on the first order (client id:
	/// <c>OrderHelpers.GetOrderClientId</c>).
	/// </summary>
	Task<PlacedOrderDto> PlaceOrderAsync(
		ContractDto contract,
		OrderDto order,
		CancellationToken cancellationToken = default
	);

	/// <summary>Asks IB to cancel an order placed by <see cref="PlaceOrderAsync"/>.</summary>
	Task CancelOrderAsync(int orderId, CancellationToken cancellationToken = default);
}
