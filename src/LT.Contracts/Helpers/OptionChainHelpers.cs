using System.Text.RegularExpressions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Options;
using LT.Contracts.Requests.Options;

namespace LT.Contracts.Helpers;

public static partial class OptionChainHelpers
{
	[GeneratedRegex(@"^\d{6}(\d{2})?$")]
	public static partial Regex ExpiryRegex();

	public static string GetExpirationsCacheKey(ContractDto underlying) =>
		$"expirations|{underlying.ConId}";

	public static string GetStrikesCacheKey(GetOptionChainRequest request) =>
		$"strikes|{request.Underlying.Symbol}|{request.Underlying.Currency}|{request.Expiry}|{request.TradingClass}";

	/// <summary>
	/// The option chain's tabs from reqSecDefOptParams: SMART entries of the symbol's own trading
	/// class and its weeklies (SPX + SPXW), skipping adjusted classes such as 2SPY; every SMART
	/// entry when none matches. A date both classes share keeps the symbol's own class.
	/// </summary>
	public static IReadOnlyList<OptionExpirationDto> SelectExpirations(
		string symbol,
		IEnumerable<OptionChainParametersDto> parameters
	)
	{
		var smart = parameters
			.Where(x => x.Exchange == MarketDataConstants.DefaultExchange)
			.ToList();
		var own = smart
			.Where(x =>
				x.TradingClass == symbol
				|| x.TradingClass == symbol + MarketDataConstants.WeeklyTradingClassSuffix
			)
			.ToList();
		return (own.Count > 0 ? own : smart)
			.OrderBy(x => x.TradingClass != symbol)
			.SelectMany(x =>
				x.Expirations.Select(e => new OptionExpirationDto
				{
					Expiration = e,
					TradingClass = x.TradingClass,
				})
			)
			.DistinctBy(x => x.Expiration)
			.OrderBy(x => x.Expiration)
			.ToList();
	}

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
