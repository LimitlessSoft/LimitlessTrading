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

	/// <summary>
	/// Client id orders go out on while connected (<c>OrderHelpers.GetOrderClientId</c>); IB
	/// cancels an order only for the client that placed it. Null when disconnected.
	/// </summary>
	int? OrderClientId { get; }
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

	/// <summary>
	/// Streaming ticks of a contract (reqMktData); <paramref name="genericTicks"/> is IB's
	/// comma-separated generic tick list (e.g. "106" for the underlying's option implied
	/// volatility), empty for the default ticks.
	/// </summary>
	IAsyncEnumerable<MarketDataTickDto> SubscribeMarketDataAsync(
		ContractDto contract,
		CancellationToken cancellationToken = default,
		string genericTicks = ""
	);

	/// <summary>
	/// Order book changes of a contract (reqMktDepth), up to <paramref name="rows"/> per side.
	/// <paramref name="isSmartDepth"/> aggregates every exchange the account has depth for (send
	/// the contract with exchange SMART); rows then name their exchange. IB allows only a few at a
	/// time — go through <c>IMarketDepthManager</c>. IB's refusals throw
	/// <see cref="Exceptions.IbkrRequestException"/> with its code.
	/// </summary>
	IAsyncEnumerable<MarketDepthUpdateDto> SubscribeMarketDepthAsync(
		ContractDto contract,
		int rows,
		bool isSmartDepth,
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
	/// IB tick-by-tick data (<c>reqTickByTickData</c>), no historical ticks; ending the
	/// enumeration cancels it. IB allows 5 at a time per account — count them through
	/// <c>IMarketDataManager.SubscribeTickByTickAsync</c>. IB refusals end the stream with an
	/// <c>IbkrRequestException</c> (10190 limit reached, 10189 no permission or Last on forex,
	/// 354 not subscribed, 200 unknown contract).
	/// </summary>
	IAsyncEnumerable<TickByTickDto> SubscribeTickByTickAsync(
		ContractDto contract,
		TickByTickType tickType,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Account summary values (<c>AccountConstants.SummaryTags</c>) of every managed account: the
	/// latest values first, then each change. All callers share one IB subscription per connection.
	/// </summary>
	IAsyncEnumerable<AccountValueDto> SubscribeAccountSummaryAsync(
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// The account's daily / unrealized / realized P&amp;L: the latest value first, then each update.
	/// All callers of one account share one IB subscription.
	/// </summary>
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

	/// <summary>News providers the account can read over the API (reqNewsProviders); SourceId empty.</summary>
	Task<IReadOnlyList<NewsProviderDto>> GetNewsProvidersAsync(
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Past headlines of a contract, newest first (reqHistoricalNews): the latest, or those strictly
	/// before <paramref name="before"/>. At most <c>NewsConstants.MaxHeadlinesPerRequest</c>.
	/// </summary>
	Task<NewsHeadlinesPageDto> GetHistoricalNewsAsync(
		int contractId,
		IReadOnlyList<string> providerCodes,
		int maxResults,
		DateTimeOffset? before = null,
		CancellationToken cancellationToken = default
	);

	Task<NewsArticleDto> GetNewsArticleAsync(
		string providerCode,
		string articleId,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Live headlines over reqMktData (one market data line; callers count it). For a contract pass
	/// its providers; for a broad tape contract (<see cref="GetBroadTapeContractsAsync"/>) none.
	/// IB replays recent headlines first.
	/// </summary>
	IAsyncEnumerable<NewsHeadlineDto> SubscribeNewsAsync(
		ContractDto contract,
		IReadOnlyList<string>? providerCodes = null,
		CancellationToken cancellationToken = default
	);

	/// <summary>The provider's broad tape contracts; empty when it has none.</summary>
	Task<IReadOnlyList<ContractDto>> GetBroadTapeContractsAsync(
		string providerCode,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Wall Street Horizon's metadata, the JSON IB sends (reqWshMetaData): the event types and
	/// filter fields. Go through <c>ICalendarManager</c>. IB's refusals throw
	/// <see cref="Exceptions.IbkrRequestException"/> with its code (10276 without the WSH
	/// subscription).
	/// </summary>
	Task<string> GetWshMetaDataAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Wall Street Horizon corporate events, the JSON IB sends (reqWshEventData). Go through
	/// <c>ICalendarManager</c>. IB's refusals throw <see cref="Exceptions.IbkrRequestException"/>
	/// with its code (10276 without the WSH subscription).
	/// </summary>
	Task<string> GetWshEventDataAsync(
		WshEventQueryDto query,
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
