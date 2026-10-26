using System.Globalization;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Enums;
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
}
