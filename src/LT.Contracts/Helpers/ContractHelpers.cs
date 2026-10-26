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

	/// <summary>Exchange to show: primary listing exchange when IB routes via SMART.</summary>
	public static string GetListingExchange(ContractDto contract) =>
		string.IsNullOrEmpty(contract.PrimaryExchange)
			? contract.Exchange
			: contract.PrimaryExchange;

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
