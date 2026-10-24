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

	/// <summary>Resolves a typed ticker to its first listing and remembers it as recent.</summary>
	Task<ContractDto> ResolveTickerAsync(
		string ticker,
		CancellationToken cancellationToken = default
	);

	/// <summary>Contracts recently picked by ticker, newest first.</summary>
	IReadOnlyList<ContractDto> GetRecentContracts();

	void AddRecentContract(ContractDto contract);
}
