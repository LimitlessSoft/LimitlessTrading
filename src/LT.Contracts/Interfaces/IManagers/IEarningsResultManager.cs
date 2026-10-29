using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Requests.Calendar;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>
/// Earnings results for the calendar's rows that have none yet, from every
/// <c>IEarningsResultSource</c> (SEC EDGAR 8-Ks, Yahoo Finance, Nasdaq), the first per symbol and
/// day kept. A failing source is logged and left out; never throws for one.
/// </summary>
public interface IEarningsResultManager
{
	/// <summary>The results of the request's symbols on its day; those without any are left out.</summary>
	Task<IReadOnlyList<EarningsResultDto>> GetResultsAsync(
		GetEarningsResultsRequest request,
		CancellationToken cancellationToken = default
	);

	/// <summary>How soon to ask again at <paramref name="now"/>: the shortest of the sources' intervals.</summary>
	TimeSpan GetPollInterval(DateTimeOffset now);
}
