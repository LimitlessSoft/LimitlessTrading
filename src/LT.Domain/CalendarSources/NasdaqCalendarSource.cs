using LT.Contracts.Constants;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Enums;
using LT.Contracts.Exceptions;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.ICalendarSources;

namespace LT.Domain.CalendarSources;

/// <summary>
/// Earnings, dividends (by ex-date), splits (by effective date) and economic releases from
/// nasdaq.com's public calendars: no IB connection, no key. Requests are planned by
/// <see cref="CalendarHelpers.GetNasdaqRequests"/> (one per calendar and day, splits once per
/// load) and sent one after another; each answer is kept in memory for
/// <see cref="CalendarConstants.EventsTtl"/>. The first failed request ends the load with that
/// failure (answers already received stay cached). Registered as a singleton.
/// </summary>
public class NasdaqCalendarSource(INasdaqApiClient nasdaqApiClient, TimeProvider timeProvider)
	: ICalendarSource
{
	private readonly SemaphoreSlim _gate = new(1, 1);
	private readonly Dictionary<
		(NasdaqCalendar Calendar, DateOnly Day),
		(IReadOnlyList<CalendarEventDto> Events, DateTimeOffset FetchedAt)
	> _answers = [];

	public CalendarSource Id => CalendarSource.Nasdaq;

	public string Name => CalendarConstants.NasdaqSourceName;

	public bool IsAvailable => true;

	public string UnavailableMessage => string.Empty;

	public async Task<CalendarSourceResultDto> GetEventsAsync(
		CalendarSourceQueryDto query,
		CancellationToken cancellationToken = default
	)
	{
		await _gate.WaitAsync(cancellationToken);
		try
		{
			var now = timeProvider.GetUtcNow();
			foreach (var key in _answers.Keys.ToList())
				if (now - _answers[key].FetchedAt > CalendarConstants.EventsTtl)
					_answers.Remove(key);

			var days = query.Days.ToHashSet();
			var events = new List<CalendarEventDto>();
			foreach (var request in CalendarHelpers.GetNasdaqRequests(query.Days, query.Filter))
			{
				if (query.Refresh || !_answers.TryGetValue(request, out var cached))
				{
					IReadOnlyList<CalendarEventDto> answer;
					try
					{
						answer = await nasdaqApiClient.GetCalendarAsync(
							request.Calendar,
							request.Day,
							cancellationToken
						);
					}
					catch (NasdaqServiceException ex)
					{
						return CalendarSourceResultDto.Failure(CalendarStatus.Failed, ex.Message);
					}
					cached = (answer, timeProvider.GetUtcNow());
					_answers[request] = cached;
				}
				// Splits come for every day from the first one on; only the range's are kept
				events.AddRange(cached.Events.Where(x => days.Contains(x.Date)));
			}
			return new CalendarSourceResultDto
			{
				Status = CalendarStatus.Loaded,
				Events = [.. events.Where(x => CalendarHelpers.Matches(x.Kind, query.Filter))],
			};
		}
		finally
		{
			_gate.Release();
		}
	}
}
