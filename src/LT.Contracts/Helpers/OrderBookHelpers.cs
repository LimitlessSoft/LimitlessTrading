using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketDepth;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;

namespace LT.Contracts.Helpers;

/// <summary>Building an order book from IB's depth updates (see docs/ibkr-tws-api.md, Market depth).</summary>
public static class OrderBookHelpers
{
	/// <summary>
	/// Applies one update to its side: an insert pushes the rows from its position down, a delete
	/// pulls them up, an update replaces the row. IB sends deletes and updates for positions past
	/// the end of a side; those change nothing. A side never grows past <paramref name="maxRows"/>.
	/// Rows are replaced, never changed in place, so a snapshot taken earlier stays as it was.
	/// Returns whether the book changed.
	/// </summary>
	public static bool Apply(
		List<OrderBookLevelDto> bids,
		List<OrderBookLevelDto> asks,
		MarketDepthUpdateDto update,
		int maxRows
	)
	{
		var side = update.Side == MarketDepthSide.Bid ? bids : asks;
		if (update.Position < 0)
			return false;

		switch (update.Operation)
		{
			case MarketDepthOperation.Insert:
				if (update.Position >= maxRows)
					return false;
				side.Insert(Math.Min(update.Position, side.Count), ToLevel(update));
				if (side.Count > maxRows)
					side.RemoveRange(maxRows, side.Count - maxRows);
				return true;
			case MarketDepthOperation.Update when update.Position < side.Count:
				side[update.Position] = ToLevel(update);
				return true;
			case MarketDepthOperation.Delete when update.Position < side.Count:
				side.RemoveAt(update.Position);
				return true;
			default:
				return false;
		}
	}

	public static OrderBookDto Snapshot(
		IEnumerable<OrderBookLevelDto> bids,
		IEnumerable<OrderBookLevelDto> asks,
		bool isSmartDepth
	) =>
		new()
		{
			Bids = [.. bids],
			Asks = [.. asks],
			IsSmartDepth = isSmartDepth,
		};

	/// <summary>Running total of the sizes from the best row out.</summary>
	public static IReadOnlyList<decimal> GetCumulativeSizes(IReadOnlyList<OrderBookLevelDto> side)
	{
		var totals = new decimal[side.Count];
		var total = 0m;
		for (var i = 0; i < side.Count; i++)
			totals[i] = total += side[i].Size;
		return totals;
	}

	/// <summary>
	/// Decimals that show every price of the book exactly (forex quotes in fifths of a pip,
	/// 1.11825): at least 2, at most 6.
	/// </summary>
	public static int GetPriceDecimals(OrderBookDto book)
	{
		var decimals = 2;
		foreach (var level in book.Bids.Concat(book.Asks))
		{
			var price = (decimal)level.Price;
			while (decimals < 6 && price != Math.Round(price, decimals))
				decimals++;
		}
		return decimals;
	}

	/// <summary>
	/// What to ask IB for: SMART depth for a contract routed through SMART when preferred,
	/// otherwise the listing exchange's depth (a contract on one exchange, e.g. crypto on PAXOS,
	/// keeps its exchange).
	/// </summary>
	public static (ContractDto Contract, bool IsSmartDepth) GetDepthContract(
		ContractDto contract,
		bool preferSmartDepth
	)
	{
		var smart = IsSmartRouted(contract) && preferSmartDepth;
		var exchange = smart
			? MarketDataConstants.DefaultExchange
			: ContractHelpers.GetListingExchange(contract);
		return (
			new ContractDto
			{
				ConId = contract.ConId,
				Symbol = contract.Symbol,
				LocalSymbol = contract.LocalSymbol,
				SecurityType = contract.SecurityType,
				Exchange = exchange,
				PrimaryExchange = contract.PrimaryExchange,
				Currency = contract.Currency,
				LastTradeDateOrContractMonth = contract.LastTradeDateOrContractMonth,
				Strike = contract.Strike,
				Right = contract.Right,
				Multiplier = contract.Multiplier,
				TradingClass = contract.TradingClass,
			},
			smart
		);
	}

	/// <summary>Routed through SMART, so SMART depth applies (crypto on PAXOS isn't).</summary>
	public static bool IsSmartRouted(ContractDto contract) =>
		string.IsNullOrEmpty(contract.Exchange)
		|| contract.Exchange == MarketDataConstants.DefaultExchange;

	/// <summary>
	/// A user-facing message for IB's depth errors (too many subscriptions, no subscription for the
	/// exchange); null for any other code.
	/// </summary>
	public static string? GetErrorMessage(int code, ContractDto depthContract) =>
		code == MarketDepthConstants.MaxSubscriptionsErrorCode
			? MarketDepthValidationCodes.MDPVC_002.GetValidationMessage(
				MarketDepthConstants.MaxSubscriptions
			)
		: MarketDepthConstants.NotSubscribedErrorCodes.Contains(code)
			? MarketDepthValidationCodes.MDPVC_003.GetValidationMessage(
				$"{ContractHelpers.GetDisplayName(depthContract)} on {depthContract.Exchange}",
				code
			)
		: null;

	private static OrderBookLevelDto ToLevel(MarketDepthUpdateDto update) =>
		new()
		{
			Price = update.Price,
			Size = update.Size,
			MarketMaker = string.IsNullOrEmpty(update.MarketMaker) ? null : update.MarketMaker,
		};
}
