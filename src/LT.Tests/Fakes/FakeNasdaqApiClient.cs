using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IApiClients;

namespace LT.Tests.Fakes;

/// <summary>Answers each Nasdaq calendar request from <see cref="Answer"/>; records what was asked.</summary>
public class FakeNasdaqApiClient : INasdaqApiClient
{
	/// <summary>The events (or a thrown exception) for a calendar and day; none by default.</summary>
	public Func<NasdaqCalendar, DateOnly, IReadOnlyList<CalendarEventDto>> Answer { get; set; } =
		(_, _) => [];

	public List<(NasdaqCalendar Calendar, DateOnly Day)> Requests { get; } = [];

	public Task<IReadOnlyList<CalendarEventDto>> GetCalendarAsync(
		NasdaqCalendar calendar,
		DateOnly day,
		CancellationToken cancellationToken = default
	)
	{
		Requests.Add((calendar, day));
		try
		{
			return Task.FromResult(Answer(calendar, day));
		}
		catch (Exception ex)
		{
			return Task.FromException<IReadOnlyList<CalendarEventDto>>(ex);
		}
	}
}
