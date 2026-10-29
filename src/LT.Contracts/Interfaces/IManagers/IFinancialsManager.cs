using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Fundamentals;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>
/// A company's income statement, balance sheet and cash flow from its SEC 10-K / 10-Q filings
/// (IB's fundamentals need an entitlement the account lacks: error 10358), kept in memory.
/// </summary>
public interface IFinancialsManager
{
	/// <summary>
	/// The contract's statements, or why there are none (<see cref="FinancialsDto.Status"/>).
	/// Never throws except on cancellation; concurrent lookups of one ticker share one request.
	/// </summary>
	Task<FinancialsDto> GetFinancialsAsync(
		ContractDto contract,
		CancellationToken cancellationToken = default
	);
}
