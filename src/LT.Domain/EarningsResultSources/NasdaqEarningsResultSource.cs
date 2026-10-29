using LT.Contracts.Constants;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IEarningsResultSources;

namespace LT.Domain.EarningsResultSources;

/// <summary>
/// Reported EPS and surprise from Nasdaq's earnings calendar, which lists them once a day is
/// past: the calendar's own Nasdaq rows already carry them when loaded, this asks again (every
/// <see cref="EarningsResultConstants.NasdaqInterval"/>) for the rows still without one, such as
/// yesterday's after-close reports before Nasdaq had them. Registered as a singleton.
/// </summary>
public class NasdaqEarningsResultSource(INasdaqApiClient nasdaqApiClient, TimeProvider timeProvider)
	: IEarningsResultSource
{
	private readonly SemaphoreSlim _gate = new(1, 1);
	private readonly Dictionary<
		DateOnly,
		(IReadOnlyList<EarningsResultDto> Results, DateTimeOffset FetchedAt)
	> _answers = [];

	public string Name => CalendarConstants.NasdaqSourceName;

	public TimeSpan GetRefreshInterval(DateTimeOffset now) =>
		EarningsResultConstants.NasdaqInterval;

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

			var events = await nasdaqApiClient.GetCalendarAsync(
				NasdaqCalendar.Earnings,
				query.Date,
				cancellationToken
			);
			IReadOnlyList<EarningsResultDto> results =
			[
				.. events
					.Where(x => x.EpsActual is not null)
					.Select(x => new EarningsResultDto
					{
						Symbol = x.Symbol,
						Date = x.Date,
						EpsActual = x.EpsActual,
						EpsSurprisePercent = x.EpsSurprisePercent,
						Source = Name,
					}),
			];
			_answers[query.Date] = (results, timeProvider.GetUtcNow());
			return results;
		}
		finally
		{
			_gate.Release();
		}
	}
}
