using System.Globalization;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>Which quote subscriptions can share one market data line.</summary>
public static class MarketDataShareHelpers
{
	/// <summary>
	/// The key of a quote line: the same contract (by conId and routing exchange, or by its
	/// fields when it has no conId), the same generic ticks and the market data type it was
	/// opened under. Subscriptions with the same key share the line.
	/// </summary>
	public static string GetShareKey(
		ContractDto contract,
		string genericTicks,
		MarketDataType? marketDataType
	)
	{
		var listing =
			contract.ConId > 0
				? $"{contract.ConId}@{Normalize(contract.Exchange)}"
				: string.Join(
					"|",
					Normalize(contract.Symbol),
					Normalize(contract.SecurityType),
					Normalize(contract.Exchange),
					Normalize(contract.PrimaryExchange),
					Normalize(contract.Currency),
					Normalize(contract.LastTradeDateOrContractMonth),
					contract.Strike.ToString("R", CultureInfo.InvariantCulture),
					Normalize(contract.Right),
					Normalize(contract.Multiplier),
					Normalize(contract.LocalSymbol),
					Normalize(contract.TradingClass)
				);
		return $"{listing}#{(genericTicks ?? string.Empty).Trim()}#{(int?)marketDataType}";
	}

	/// <summary>The owners of a shared line, each once, in the order given; empty ones dropped.</summary>
	public static IReadOnlyList<string> GetOwners(IEnumerable<string> owners) =>
		[.. owners.Where(x => !string.IsNullOrEmpty(x)).Distinct(StringComparer.Ordinal)];

	/// <summary>
	/// The list entry of a shared line with its current subscribers' owners (each once, joined
	/// for <see cref="MarketDataStreamDto.Owner"/>) and their count.
	/// </summary>
	public static MarketDataStreamDto WithSubscribers(
		MarketDataStreamDto stream,
		IEnumerable<string> subscriberOwners
	)
	{
		var all = subscriberOwners.ToList();
		var owners = GetOwners(all);
		return new MarketDataStreamDto
		{
			Id = stream.Id,
			Kind = stream.Kind,
			Contract = stream.Contract,
			GenericTicks = stream.GenericTicks,
			Owner = string.Join(", ", owners),
			Owners = owners,
			Subscribers = all.Count,
			OpenedAt = stream.OpenedAt,
		};
	}

	private static string Normalize(string? value) =>
		(value ?? string.Empty).Trim().ToUpperInvariant();
}
