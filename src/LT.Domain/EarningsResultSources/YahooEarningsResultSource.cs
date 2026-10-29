using LT.Contracts.Constants;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IEarningsResultSources;

namespace LT.Domain.EarningsResultSources;

/// <summary>
/// Reported EPS and surprise from Yahoo Finance's earnings calendar, which has them the same day
/// (Nasdaq only from the next). A day's pages (largest companies first, at most
/// <see cref="YahooConstants.MaxPagesPerDay"/>) are read one after another and kept for
/// <see cref="EarningsResultConstants.YahooInterval"/> today, longer for past days. Later days
/// have no results and are not asked for. Registered as a singleton.
/// </summary>
public class YahooEarningsResultSource(IYahooApiClient yahooApiClient, TimeProvider timeProvider)
	: IEarningsResultSource
{
	private readonly SemaphoreSlim _gate = new(1, 1);
	private readonly Dictionary<
		DateOnly,
		(IReadOnlyList<EarningsResultDto> Results, DateTimeOffset FetchedAt)
	> _answers = [];

	public string Name => YahooConstants.SourceName;

	public TimeSpan GetRefreshInterval(DateTimeOffset now) => EarningsResultConstants.YahooInterval;

	public async Task<IReadOnlyList<EarningsResultDto>> GetResultsAsync(
		EarningsResultQueryDto query,
		CancellationToken cancellationToken = default
	)
	{
		var now = timeProvider.GetUtcNow();
		var today = CalendarHelpers.GetToday(now);
		if (query.Date > today)
			return [];
		await _gate.WaitAsync(cancellationToken);
		try
		{
			var ttl =
				query.Date == today ? GetRefreshInterval(now) : EarningsResultConstants.PastDayTtl;
			if (
				!query.Refresh
				&& _answers.TryGetValue(query.Date, out var cached)
				&& now - cached.FetchedAt < ttl
			)
				return cached.Results;

			var results = new List<EarningsResultDto>();
			var offset = 0;
			for (var page = 0; page < YahooConstants.MaxPagesPerDay; page++)
			{
				var answer = await yahooApiClient.GetEarningsPageAsync(
					query.Date,
					offset,
					cancellationToken
				);
				// A page that isn't the one asked for (Yahoo ignoring the offset) repeats rows
				if (page > 0 && answer.First != offset + 1)
					break;
				results.AddRange(answer.Rows.Where(x => x.HasFigures));
				offset += answer.Rows.Count;
				if (
					answer.Rows.Count < YahooConstants.PageSize
					|| answer.Total > 0 && offset >= answer.Total
				)
					break;
			}
			_answers[query.Date] = (results, timeProvider.GetUtcNow());
			return results;
		}
		finally
		{
			_gate.Release();
		}
	}
}
