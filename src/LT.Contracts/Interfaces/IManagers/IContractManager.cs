using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Requests.Contracts;

namespace LT.Contracts.Interfaces.IManagers;

public interface IContractManager
{
	Task<IReadOnlyList<ContractDetailsDto>> SearchAsync(
		ContractSearchRequest request,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Every stock / ETF, index and crypto listing of a symbol (ticker boxes), in that order, US
	/// dollar listings first. Fails only when no type could be searched.
	/// </summary>
	Task<IReadOnlyList<ContractDetailsDto>> SearchSymbolAsync(
		string symbol,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Every listing IB has for a contract known only by its fields, not its conId (a hand-made
	/// watchlist file): with no security type, <see cref="SearchSymbolAsync"/>; else one search
	/// with the symbol and whatever else is given (exchange, currency, expiry, strike, right,
	/// multiplier, trading class, local symbol), stocks via SMART. Cached like the searches;
	/// <c>ContractHelpers.PickMatch</c> picks the one meant.
	/// </summary>
	Task<IReadOnlyList<ContractDetailsDto>> FindAsync(
		ContractDto contract,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// The details of a resolved contract (conId &gt; 0, else it throws): names, classification,
	/// trading hours, identifiers. Null when IB has none for the conId. Cached for
	/// <c>CacheConstants.TradingHoursTtl</c>, as the hours IB lists move with the days.
	/// </summary>
	Task<ContractDetailsDto?> GetDetailsAsync(
		ContractDto contract,
		CancellationToken cancellationToken = default
	);

	/// <summary>Resolves a typed ticker to its first listing and remembers it as recent.</summary>
	Task<ContractDto> ResolveTickerAsync(
		string ticker,
		CancellationToken cancellationToken = default
	);

	/// <summary>Contracts recently picked by ticker, newest first.</summary>
	IReadOnlyList<ContractDto> GetRecentContracts();

	void AddRecentContract(ContractDto contract);
}
