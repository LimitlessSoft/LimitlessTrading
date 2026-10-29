using System.Globalization;
using System.Runtime.CompilerServices;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Dtos.Positions;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Requests.Orders;

namespace LT.Contracts.Helpers;

public static class OrderHelpers
{
	/// <summary>Client id of the order connection: the main one plus an offset, within IB's range.</summary>
	public static int GetOrderClientId(int clientId) =>
		(clientId + OrderConstants.OrderClientIdOffset) % (ConnectionConstants.ClientIdMax + 1);

	/// <summary>
	/// Whether this app can cancel an open order: IB takes a cancel only from the client that
	/// placed it (the order connection), and not again once one is under way.
	/// </summary>
	public static bool CanCancel(OpenOrderDto order, int? orderClientId) =>
		orderClientId is { } clientId
		&& order.ClientId == clientId
		&& order.Status is not ("PendingCancel" or "Cancelled" or "ApiCancelled" or "Filled");

	/// <summary>An order status after which the order works no more (Filled, Cancelled, ApiCancelled, Inactive).</summary>
	public static bool IsDone(string status) =>
		status is OrderConstants.FilledStatus or "Cancelled" or "ApiCancelled" or "Inactive";

	/// <summary>
	/// Why this app can't change the price of <paramref name="order"/>, null when it can: IB takes
	/// a change only from the client that placed the order (the order connection,
	/// <paramref name="orderClientId"/>, null while disconnected), only a limit order's limit price
	/// is changed here, and not once it is done or being cancelled.
	/// </summary>
	public static OrderValidationCodes? GetModifyBlock(OpenOrderDto order, int? orderClientId) =>
		orderClientId is not { } clientId || order.ClientId != clientId
			? OrderValidationCodes.OVC_014
		: order.OrderType != OrderConstants.LimitOrderType || order.LimitPrice is not > 0
			? OrderValidationCodes.OVC_015
		: IsDone(order.Status) || order.Status == "PendingCancel" ? OrderValidationCodes.OVC_016
		: null;

	/// <summary>
	/// One line to confirm a new limit price before sending it, e.g. "SELL 1 SPY 20261029 767 P,
	/// limit 1.60 → 1.55, DAY, account U1234567 (order 44)".
	/// </summary>
	public static string DescribeModification(OpenOrderDto order, double newLimitPrice)
	{
		var quantity = order.Quantity.ToString("#,0.########", CultureInfo.InvariantCulture);
		var filled =
			order.Filled > 0
				? $" ({order.Filled.ToString("#,0.########", CultureInfo.InvariantCulture)} filled)"
				: string.Empty;
		var from = order.LimitPrice is { } limit ? FormatPrice(limit) : "?";
		return $"{order.Action} {quantity}{filled} {order.Symbol}, limit {from} → {FormatPrice(newLimitPrice)}, {order.TimeInForce}, account {order.Account} (order {order.OrderId})";
	}

	/// <summary>A price with at least two decimals and no trailing zeros past them: 1.6 → "1.60", 1.555 → "1.555".</summary>
	public static string FormatPrice(double price) =>
		price.ToString("#,0.00######", CultureInfo.InvariantCulture);

	/// <summary>
	/// Time in force values an order may use, the default first. Crypto takes GTC or IOC only
	/// (IB error 10052), and a crypto market order fills now or never (IOC).
	/// </summary>
	public static IReadOnlyList<TimeInForce> GetTimeInForces(string securityType, OrderType type) =>
		securityType == MarketDataConstants.CryptoType
			? type == OrderType.Market
				? [TimeInForce.Ioc]
				: [TimeInForce.Gtc, TimeInForce.Ioc]
			: [TimeInForce.Day, TimeInForce.Gtc, TimeInForce.Ioc];

	/// <summary>
	/// The order that flattens <paramref name="position"/>: sell a long, buy back a short, the whole
	/// size, in the position's account. Null for a flat position.
	/// </summary>
	public static OrderDraftDto? GetClosingDraft(PositionDto position)
	{
		if (position.Quantity == 0)
			return null;
		var quantity = Math.Abs(position.Quantity);
		var side = position.Quantity > 0 ? "long" : "short";
		return new()
		{
			Contract = position.Contract,
			Account = position.Account,
			Action = position.Quantity > 0 ? OrderAction.Sell : OrderAction.Buy,
			Quantity = quantity,
			Note =
				$"Closes the {side} position of {FormatInput(quantity)} in {position.Account}. Review, then send.",
		};
	}

	/// <summary>
	/// The quick exit of <paramref name="position"/>: a market order for the whole size (sell a long,
	/// buy back a short) in the position's account, sent without review. Time in force is the
	/// default for a market order of that security type (DAY; IOC for crypto). Null for a flat
	/// position.
	/// </summary>
	public static PlaceOrderRequest? GetQuickExitRequest(PositionDto position) =>
		position.Quantity == 0
			? null
			: new()
			{
				Contract = position.Contract,
				Account = position.Account,
				Action = position.Quantity > 0 ? OrderAction.Sell : OrderAction.Buy,
				OrderType = OrderType.Market,
				Quantity = Math.Abs(position.Quantity),
				TimeInForce = GetTimeInForces(position.Contract.SecurityType, OrderType.Market)[0],
			};

	public static OrderDto ToOrder(PlaceOrderRequest request) =>
		new()
		{
			Account = request.Account,
			Action = request.Action,
			OrderType = request.OrderType,
			Quantity = request.Quantity ?? 0,
			CashQuantity = request.CashAmount,
			LimitPrice = request.OrderType == OrderType.Limit ? request.LimitPrice : null,
			TimeInForce = request.TimeInForce,
			OutsideRegularHours = request.OutsideRegularHours,
		};

	/// <summary>
	/// One line to confirm before sending, e.g. "Buy $10.00 of BTC.USD (PAXOS) at market, IOC,
	/// account U1234567".
	/// </summary>
	public static string Describe(PlaceOrderRequest request)
	{
		var contract = request.Contract;
		var name =
			contract == null
				? "?"
				: $"{ContractHelpers.GetDisplayName(contract)} ({ContractHelpers.GetListingExchange(contract)})";
		var size = request.CashAmount is { } cash
			? $"{FormatMoney(cash)} of"
			: (request.Quantity ?? 0).ToString("#,0.########", CultureInfo.InvariantCulture);
		var price =
			request.OrderType == OrderType.Limit && request.LimitPrice is { } limit
				? $"limit {limit.ToString("#,0.########", CultureInfo.InvariantCulture)}"
				: "at market";
		return $"{request.Action} {size} {name} {price}, {request.TimeInForce.ToString().ToUpperInvariant()}, account {request.Account}";
	}

	/// <summary>The price an order would likely fill near: the ask for a buy, the bid for a sell, else the last.</summary>
	public static double? GetReferencePrice(
		OrderAction action,
		double? bid,
		double? ask,
		double? last
	) =>
		(action == OrderAction.Buy ? ask : bid) is > 0 and var side ? side
		: last is > 0 ? last
		: null;

	/// <summary>
	/// What the ticket's size comes to at <paramref name="price"/>: units for an amount, money for a
	/// quantity, e.g. "≈ 0.00012500 units" / "≈ $10.00"; null when it can't be estimated.
	/// </summary>
	public static string? Estimate(decimal? quantity, decimal? cashAmount, double? price)
	{
		if (price is not > 0)
			return null;
		if (cashAmount is > 0 and var cash)
			return $"≈ {(cash / (decimal)price.Value).ToString("#,0.########", CultureInfo.InvariantCulture)} units";
		if (quantity is > 0 and var units)
			return $"≈ {FormatMoney(units * (decimal)price.Value)}";
		return null;
	}

	public static string FormatMoney(decimal amount) =>
		amount.ToString("$#,0.00", CultureInfo.InvariantCulture);

	/// <summary>A number as the ticket's text boxes take it back (<see cref="ParseNumber"/>), e.g. "0.5".</summary>
	public static string FormatInput(decimal value) =>
		value.ToString("0.########", CultureInfo.InvariantCulture);

	/// <summary>Parses a typed number ("10", "0.0002", "1,000.5"); null when empty or not a number.</summary>
	public static decimal? ParseNumber(string? text) =>
		decimal.TryParse(
			text?.Trim().Replace(",", string.Empty),
			NumberStyles.Number,
			CultureInfo.InvariantCulture,
			out var value
		)
			? value
			: null;

	/// <summary>
	/// Whether a status reports a fill the order's earlier statuses didn't: more filled quantity
	/// than <paramref name="previousFilled"/>, or a first Filled status with no quantity reported
	/// before. Submitted, or Cancelled with nothing filled, is not one.
	/// </summary>
	public static bool IsNewFill(OrderStatusDto status, decimal previousFilled, bool filledSeen) =>
		status.Filled > previousFilled
		|| (status.Status == OrderConstants.FilledStatus && previousFilled == 0 && !filledSeen);

	/// <summary>
	/// Passes an order's statuses through and calls <paramref name="onFill"/> for each that
	/// reports a new fill (<see cref="IsNewFill"/>); an <paramref name="onFill"/> that throws never
	/// ends the stream.
	/// </summary>
	public static async IAsyncEnumerable<OrderStatusDto> OnFillsAsync(
		IAsyncEnumerable<OrderStatusDto> statuses,
		Action<OrderStatusDto> onFill,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		decimal filled = 0;
		var filledSeen = false;
		await foreach (
			var status in statuses.WithCancellation(cancellationToken).ConfigureAwait(false)
		)
		{
			if (IsNewFill(status, filled, filledSeen))
				try
				{
					onFill(status);
				}
				catch (Exception)
				{
					// Following the order matters more than what listens for its fills
				}
			filled = Math.Max(filled, status.Filled);
			filledSeen |= status.Status == OrderConstants.FilledStatus;
			yield return status;
		}
	}
}
