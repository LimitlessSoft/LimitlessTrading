using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Enums.ValidationCodes;

namespace LT.Contracts.Helpers;

/// <summary>Working orders on a chart: their price lines and dragging them to a new price.</summary>
public static class ChartOrderHelpers
{
	/// <summary>
	/// The lines of the working orders of <paramref name="conId"/> (in <paramref name="account"/>,
	/// or every account when null): one at the limit price, and one at the stop price of a
	/// stop-type order (<see cref="OrderConstants.StopOrderTypes"/>). Orders that are done draw
	/// nothing. <paramref name="getMoveBlock"/> tells why an order can't be moved (null: it can);
	/// only the limit line of a limit order ever moves. <paramref name="pending"/> puts a line at a
	/// new price that is being confirmed or sent.
	/// </summary>
	public static IReadOnlyList<ChartOrderLineDto> GetOrderLines(
		IEnumerable<OpenOrderDto> orders,
		int conId,
		string? account,
		Func<OpenOrderDto, string?> getMoveBlock,
		(string LineId, double Price)? pending = null
	)
	{
		var lines = new List<ChartOrderLineDto>();
		if (conId <= 0)
			return lines;
		foreach (var order in orders)
		{
			if (
				order.Contract.ConId != conId
				|| (account != null && order.Account != account)
				|| OrderHelpers.IsDone(order.Status)
			)
				continue;
			var block = getMoveBlock(order);
			if (order.LimitPrice is > 0 and var limit)
				lines.Add(ToLine(order, limit, false, block, pending));
			if (
				OrderConstants.StopOrderTypes.Contains(order.OrderType)
				&& order.AuxPrice is > 0 and var stop
			)
				lines.Add(
					ToLine(
						order,
						stop,
						true,
						OrderValidationCodes.OVC_015.GetValidationMessage(),
						pending
					)
				);
		}
		return lines;
	}

	/// <summary>Identifies one price line of an order across reloads.</summary>
	public static string GetLineId(OpenOrderDto order, bool isStopPrice) =>
		$"{order.PermId}:{order.ClientId}:{order.OrderId}:{(isStopPrice ? "stop" : "limit")}";

	/// <summary>
	/// <paramref name="price"/> on the nearest multiple of <paramref name="tick"/> (the contract's
	/// minimum tick), at least one tick; rounded to the tick's decimals so 0.1 + 0.2 stays 0.3.
	/// A tick that isn't a positive number leaves the price as it is.
	/// </summary>
	public static double SnapToTick(double price, double tick)
	{
		if (!(tick > 0) || !double.IsFinite(tick) || !double.IsFinite(price))
			return price;
		var ticks = Math.Max(1, Math.Round(price / tick, MidpointRounding.AwayFromZero));
		return Math.Round(ticks * tick, GetDecimals(tick));
	}

	/// <summary>The decimals a tick needs: 2 for 0.01, 4 for 0.0001, 2 for 0.25, 0 for 5.</summary>
	public static int GetDecimals(double tick)
	{
		for (var decimals = 0; decimals < 10; decimals++)
		{
			var scaled = tick * Math.Pow(10, decimals);
			if (Math.Abs(scaled - Math.Round(scaled)) < 1e-9 * Math.Max(1, scaled))
				return decimals;
		}
		return 10;
	}

	private static ChartOrderLineDto ToLine(
		OpenOrderDto order,
		double price,
		bool isStopPrice,
		string? block,
		(string LineId, double Price)? pending
	)
	{
		var id = GetLineId(order, isStopPrice);
		var isPending = pending is { } p && p.LineId == id;
		return new ChartOrderLineDto
		{
			Id = id,
			OrderId = order.OrderId,
			PermId = order.PermId,
			Action = order.Action,
			IsBuy = order.Action == "BUY",
			Quantity = Math.Max(0, order.Quantity - order.Filled),
			OrderType = order.OrderType,
			Price = isPending ? pending!.Value.Price : price,
			IsStopPrice = isStopPrice,
			MoveBlockedReason = block,
			PendingFromPrice = isPending ? price : null,
		};
	}
}
