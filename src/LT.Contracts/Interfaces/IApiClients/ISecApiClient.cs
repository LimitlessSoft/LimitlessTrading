using LT.Contracts.Dtos.Fundamentals;

namespace LT.Contracts.Interfaces.IApiClients;

/// <summary>
/// SEC EDGAR's public JSON (no account, no key). Requests are serialized and spaced within the
/// SEC's 10 per second, with the User-Agent it requires.
/// </summary>
public interface ISecApiClient
{
	/// <summary>Ticker (upper case, class with a dash: BRK-B) to CIK.</summary>
	Task<IReadOnlyDictionary<string, int>> GetTickerCiksAsync(
		CancellationToken cancellationToken = default
	);

	/// <summary>The company's dei:EntityCommonStockSharesOutstanding values; null when it has none.</summary>
	Task<IReadOnlyList<SecFactDto>?> GetSharesOutstandingFactsAsync(
		int cik,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// From the company's full facts: dei:EntityCommonStockSharesOutstanding and
	/// us-gaap:CommonStockSharesOutstanding (multi-class companies report only the latter
	/// undivided); null when the SEC has no facts for the CIK.
	/// </summary>
	Task<IReadOnlyList<SecFactDto>?> GetCompanyShareFactsAsync(
		int cik,
		CancellationToken cancellationToken = default
	);
}
