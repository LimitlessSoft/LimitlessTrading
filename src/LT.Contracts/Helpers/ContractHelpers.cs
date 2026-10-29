using System.Globalization;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Entities;

namespace LT.Contracts.Helpers;

public static class ContractHelpers
{
	/// <summary>
	/// US stocks a fresh install starts with (SPY, MSFT, NVDA, TSLA): the first watchlist, and SPY
	/// on the first chart. New instances on every call.
	/// </summary>
	public static List<ContractEntity> GetStarterContracts() =>
		[
			CreateSmartStock(756733, "SPY", "ARCA", "SPY"),
			CreateSmartStock(272093, "MSFT", "NASDAQ", "NMS"),
			CreateSmartStock(4815747, "NVDA", "NASDAQ", "NMS"),
			CreateSmartStock(76792991, "TSLA", "NASDAQ", "NMS"),
		];

	/// <summary>The starter chart's contract: SPY (ARCA, USD) routed via SMART.</summary>
	public static ContractEntity GetStarterChartContract() => GetStarterContracts()[0];

	/// <summary>A USD stock routed via SMART, as IB returns it from a contract search.</summary>
	public static ContractEntity CreateSmartStock(
		int conId,
		string symbol,
		string primaryExchange,
		string tradingClass
	) =>
		new()
		{
			ConId = conId,
			Symbol = symbol,
			LocalSymbol = symbol,
			SecurityType = MarketDataConstants.StockType,
			Exchange = MarketDataConstants.DefaultExchange,
			PrimaryExchange = primaryExchange,
			Currency = MarketDataConstants.PreferredCurrency,
			TradingClass = tradingClass,
		};

	/// <summary>Cache key of one contract search (an already normalised query).</summary>
	public static string GetSearchCacheKey(ContractDto query) =>
		$"search|{query.Symbol}|{query.SecurityType}|{query.Exchange}|{query.Currency}";

	/// <summary>
	/// The search that finds a contract known by its fields (<c>IContractManager.FindAsync</c>):
	/// no conId, trimmed and upper-cased, stocks via SMART (their given exchange or primary
	/// exchange only steers <see cref="PickMatch"/>: a wrong one would fail the search), other
	/// types on the given exchange or every one.
	/// </summary>
	public static ContractDto ToFindQuery(ContractDto contract)
	{
		var type = Normalize(contract.SecurityType);
		return new ContractDto
		{
			Symbol = Normalize(contract.Symbol),
			SecurityType = type,
			Exchange =
				type == MarketDataConstants.StockType
					? MarketDataConstants.DefaultExchange
					: Normalize(contract.Exchange),
			Currency = Normalize(contract.Currency),
			LocalSymbol = contract.LocalSymbol?.Trim() ?? string.Empty,
			LastTradeDateOrContractMonth =
				contract.LastTradeDateOrContractMonth?.Trim() ?? string.Empty,
			Strike = contract.Strike > 0 ? contract.Strike : 0,
			Right = Normalize(contract.Right),
			Multiplier = contract.Multiplier?.Trim() ?? string.Empty,
			TradingClass = Normalize(contract.TradingClass),
		};
	}

	/// <summary>Cache key of a <see cref="ToFindQuery"/> search.</summary>
	public static string GetFindCacheKey(ContractDto query) =>
		string.Join(
			'|',
			"find",
			query.Symbol,
			query.SecurityType,
			query.Exchange,
			query.Currency,
			query.LocalSymbol,
			query.LastTradeDateOrContractMonth,
			query.Strike.ToString(CultureInfo.InvariantCulture),
			query.Right,
			query.Multiplier,
			query.TradingClass
		);

	/// <summary>
	/// The listing a contract known by its fields means, among what IB found for it; null when
	/// none fits or it stays ambiguous. Every given field must match (expiry by prefix, so a
	/// month finds its date; right by its first letter). Then, while several are left: with no
	/// security type the first type found (stock before index before crypto), with no currency
	/// US dollar, the given exchange or primary exchange. Several conIds left is ambiguous; of
	/// one conId's venues the given exchange, else SMART, else the first.
	/// </summary>
	public static ContractDto? PickMatch(
		ContractDto wanted,
		IReadOnlyList<ContractDetailsDto> found
	)
	{
		var candidates = found
			.Select(x => x.Contract)
			.Where(x => x.ConId > 0 && IsMatch(wanted, x))
			.ToList();
		if (candidates.Count == 0)
			return null;

		if (Normalize(wanted.SecurityType).Length == 0)
		{
			var type = candidates[0].SecurityType;
			candidates = [.. candidates.Where(x => x.SecurityType == type)];
		}
		if (Normalize(wanted.Currency).Length == 0)
			candidates = Prefer(
				candidates,
				x => x.Currency == MarketDataConstants.PreferredCurrency
			);
		var exchanges = new[] { Normalize(wanted.Exchange), Normalize(wanted.PrimaryExchange) }
			.Where(x => x.Length > 0 && x != MarketDataConstants.DefaultExchange)
			.ToList();
		if (exchanges.Count > 0)
			candidates = Prefer(
				candidates,
				x =>
					exchanges.Contains(Normalize(x.Exchange))
					|| exchanges.Contains(Normalize(x.PrimaryExchange))
			);
		if (candidates.Select(x => x.ConId).Distinct().Count() != 1)
			return null;

		return candidates.FirstOrDefault(x => exchanges.Contains(Normalize(x.Exchange)))
			?? candidates.FirstOrDefault(x =>
				Normalize(x.Exchange) == MarketDataConstants.DefaultExchange
			)
			?? candidates[0];
	}

	private static bool IsMatch(ContractDto wanted, ContractDto candidate)
	{
		static bool Same(string? given, string? actual) =>
			Normalize(given).Length == 0 || Normalize(given) == Normalize(actual);

		var expiry = wanted.LastTradeDateOrContractMonth?.Trim() ?? string.Empty;
		var right = Normalize(wanted.Right);
		var multiplier = GetMultiplier(wanted);
		var localSymbol = (wanted.LocalSymbol ?? string.Empty).Replace(" ", string.Empty);
		return Same(wanted.SecurityType, candidate.SecurityType)
			&& Same(wanted.Currency, candidate.Currency)
			&& Same(wanted.TradingClass, candidate.TradingClass)
			&& (
				expiry.Length == 0
				|| (candidate.LastTradeDateOrContractMonth ?? string.Empty).StartsWith(
					expiry,
					StringComparison.Ordinal
				)
			)
			&& (wanted.Strike <= 0 || Math.Abs(wanted.Strike - candidate.Strike) < 1e-6)
			&& (right.Length == 0 || Normalize(candidate.Right).StartsWith(right[0]))
			&& (multiplier is null || GetMultiplier(candidate) == multiplier)
			&& (
				localSymbol.Length == 0
				|| string.Equals(
					localSymbol,
					(candidate.LocalSymbol ?? string.Empty).Replace(" ", string.Empty),
					StringComparison.OrdinalIgnoreCase
				)
			);
	}

	// Narrows to those that qualify, unless none does
	private static List<ContractDto> Prefer(
		List<ContractDto> contracts,
		Func<ContractDto, bool> qualifies
	)
	{
		var preferred = contracts.Where(qualifies).ToList();
		return preferred.Count > 0 ? preferred : contracts;
	}

	private static string Normalize(string? value) =>
		value?.Trim().ToUpperInvariant() ?? string.Empty;

	/// <summary>Cache key of a contract's details read for its trading hours.</summary>
	public static string GetTradingHoursCacheKey(int conId) => $"hours|{conId}";

	/// <summary>Cache key of a contract's details read whole (the Overview widget).</summary>
	public static string GetDetailsCacheKey(int conId) => $"details|{conId}";

	/// <summary>Cache key of a ticker box's search of every type.</summary>
	public static string GetSymbolSearchCacheKey(string symbol) =>
		$"symbol|{symbol.Trim().ToUpperInvariant()}";

	public static string GetDisplayName(ContractDto contract) =>
		contract.SecurityType switch
		{
			MarketDataConstants.OptionType =>
				$"{contract.Symbol} {contract.LastTradeDateOrContractMonth} {contract.Strike.ToString("0.##", CultureInfo.InvariantCulture)} {contract.Right}",
			MarketDataConstants.StockType or MarketDataConstants.IndexType => contract.Symbol,
			_ => string.IsNullOrEmpty(contract.LocalSymbol)
				? contract.Symbol
				: contract.LocalSymbol,
		};

	/// <summary>
	/// The contract multiplier as IB reports it (100 for most equity options); null when IB sent
	/// none or it isn't a positive number.
	/// </summary>
	public static double? GetMultiplier(ContractDto contract) =>
		double.TryParse(
			contract.Multiplier,
			NumberStyles.Float,
			CultureInfo.InvariantCulture,
			out var value
		)
		&& value > 0
			? value
			: null;

	/// <summary>Exchange to show: primary listing exchange when IB routes via SMART.</summary>
	public static string GetListingExchange(ContractDto contract) =>
		string.IsNullOrEmpty(contract.PrimaryExchange)
			? contract.Exchange
			: contract.PrimaryExchange;

	/// <summary>An option (OPT) or an option on a future (FOP); case and spaces ignored.</summary>
	public static bool IsOption(string? securityType) =>
		Normalize(securityType)
			is MarketDataConstants.OptionType
				or MarketDataConstants.FutureOptionType;

	public static bool SupportsOptionChain(ContractDto contract) =>
		contract.SecurityType is MarketDataConstants.StockType or MarketDataConstants.IndexType;

	/// <summary>Same instrument: by conId when both have one, else by symbol, type and currency.</summary>
	public static bool IsSameContract(ContractDto a, ContractDto b) =>
		a.ConId > 0 && b.ConId > 0
			? a.ConId == b.ConId
			: string.Equals(a.Symbol, b.Symbol, StringComparison.OrdinalIgnoreCase)
				&& a.SecurityType == b.SecurityType
				&& a.Currency == b.Currency;

	/// <summary>
	/// Same listing: same conId on the same exchange. One conId can trade on several venues
	/// (crypto on PAXOS and ZEROHASH), each its own listing.
	/// </summary>
	public static bool IsSameListing(ContractDto a, ContractDto b) =>
		a.ConId == b.ConId
		&& string.Equals(a.Exchange, b.Exchange, StringComparison.OrdinalIgnoreCase);

	/// <summary>Puts <paramref name="contract"/> first, drops its older entry, keeps at most <paramref name="max"/>.</summary>
	public static List<ContractDto> PushRecent(
		IEnumerable<ContractDto> recent,
		ContractDto contract,
		int max
	) => recent.Where(x => !IsSameContract(x, contract)).Prepend(contract).Take(max).ToList();
}
