using LT.Contracts.Dtos.Calendar;
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
	/// us-gaap:CommonStockSharesOutstanding (some multi-class companies report only the latter
	/// undivided), and the us-gaap weighted averages, basic and diluted (with their
	/// <see cref="SecFactDto.Start"/>); null when the SEC has no facts for the CIK.
	/// </summary>
	Task<IReadOnlyList<SecFactDto>?> GetCompanyShareFactsAsync(
		int cik,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// The dei:EntityCommonStockSharesOutstanding counts on the cover page of the company's latest
	/// inline-XBRL 10-K / 10-Q, per <see cref="SecFactDto.ShareClass"/> (multi-class filers tag only
	/// these, which the JSON APIs leave out). Two requests, the second several MB. Null when the
	/// company has no such filing.
	/// </summary>
	Task<IReadOnlyList<SecFactDto>?> GetLatestFilingShareFactsAsync(
		int cik,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// A page of EDGAR's latest filings feed (<c>getcurrent</c>) for <paramref name="form"/>,
	/// newest first, from position <paramref name="start"/> (at most
	/// <see cref="Constants.SecConstants.CurrentFilingsPageSize"/> entries; about 400 8-Ks a day).
	/// </summary>
	Task<IReadOnlyList<SecCurrentFilingDto>> GetCurrentFilingsAsync(
		string form,
		int start,
		CancellationToken cancellationToken = default
	);
}
