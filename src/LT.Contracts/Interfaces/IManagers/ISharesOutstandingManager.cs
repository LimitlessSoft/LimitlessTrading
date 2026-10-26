using LT.Contracts.Dtos.Contracts;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>Shares outstanding for market cap, from SEC filings, cached on disk.</summary>
public interface ISharesOutstandingManager
{
	/// <summary>
	/// The contract's shares outstanding, or null: not a US stock, the SEC has nothing usable,
	/// or the SEC can't be reached (then a stale cached count is used when there is one). Never
	/// throws except on cancellation; concurrent lookups of one ticker share one request.
	/// </summary>
	Task<long?> GetSharesOutstandingAsync(
		ContractDto contract,
		CancellationToken cancellationToken = default
	);
}
