using System.Text.RegularExpressions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Options;

namespace LT.Contracts.Helpers;

public static partial class OptionChainHelpers
{
	[GeneratedRegex(@"^\d{6}(\d{2})?$")]
	public static partial Regex ExpiryRegex();

	/// <summary>Pairs calls and puts by expiration and strike.</summary>
	public static IReadOnlyList<OptionStrikeDto> BuildStrikes(IEnumerable<ContractDto> options) =>
		options
			.Where(x => x.SecurityType == MarketDataConstants.OptionType)
			.GroupBy(x => (x.LastTradeDateOrContractMonth, x.Strike))
			.Select(g => new OptionStrikeDto
			{
				Expiration = g.Key.LastTradeDateOrContractMonth,
				Strike = g.Key.Strike,
				Call = g.FirstOrDefault(x => x.Right == MarketDataConstants.CallRight),
				Put = g.FirstOrDefault(x => x.Right == MarketDataConstants.PutRight),
			})
			.OrderBy(x => x.Expiration)
			.ThenBy(x => x.Strike)
			.ToList();

	/// <summary>
	/// The <paramref name="count"/> strikes on each side of the price (the whole list centred on
	/// the middle strike when the price is unknown).
	/// </summary>
	public static IReadOnlyList<OptionStrikeDto> AroundPrice(
		IReadOnlyList<OptionStrikeDto> strikes,
		double? price,
		int count
	)
	{
		if (strikes.Count == 0)
			return [];

		var center = price is { } p
			? strikes
				.Select((x, i) => (Distance: Math.Abs(x.Strike - p), Index: i))
				.MinBy(x => x.Distance)
				.Index
			: strikes.Count / 2;
		var from = Math.Max(0, center - count);
		var to = Math.Min(strikes.Count - 1, center + count);
		return strikes.Skip(from).Take(to - from + 1).ToList();
	}
}
