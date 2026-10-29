using LT.Contracts.Dtos.Calendar;

namespace LT.Contracts.Interfaces.IEarningsResultSources;

/// <summary>
/// Somewhere earnings results come from (Nasdaq's past days, Yahoo Finance, SEC EDGAR's 8-Ks).
/// <c>IEarningsResultManager</c> asks every source and keeps the first result per symbol and day
/// (<see cref="Helpers.EarningsResultHelpers.Merge"/>). A source keeps its own answers and asks
/// its service again only once its <see cref="GetRefreshInterval"/> has passed; one failing
/// doesn't hide the others'.
/// </summary>
public interface IEarningsResultSource
{
	/// <summary>Shown in the row's tooltip: "Yahoo Finance".</summary>
	string Name { get; }

	/// <summary>
	/// How long an answer for today is good for at <paramref name="now"/> (shorter while results
	/// come out); the calendar polls at the shortest of the sources'.
	/// </summary>
	TimeSpan GetRefreshInterval(DateTimeOffset now);

	/// <summary>
	/// The results it knows of the query's day (any symbols; the caller matches them). Throws
	/// when its service fails or answers in a shape it can't read.
	/// </summary>
	Task<IReadOnlyList<EarningsResultDto>> GetResultsAsync(
		EarningsResultQueryDto query,
		CancellationToken cancellationToken = default
	);
}
