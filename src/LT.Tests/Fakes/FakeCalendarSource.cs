using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.ICalendarSources;

namespace LT.Tests.Fakes;

/// <summary>A calendar source answering <see cref="Result"/> (or throwing <see cref="Error"/>); records the queries.</summary>
public class FakeCalendarSource(CalendarSource id) : ICalendarSource
{
	public CalendarSource Id => id;
	public string Name => id.ToString();
	public bool IsAvailable { get; set; } = true;
	public string UnavailableMessage { get; set; } = "Not available.";

	public CalendarSourceResultDto Result { get; set; } = new() { Status = CalendarStatus.Loaded };
	public Exception? Error { get; set; }
	public List<CalendarSourceQueryDto> Queries { get; } = [];

	public Task<CalendarSourceResultDto> GetEventsAsync(
		CalendarSourceQueryDto query,
		CancellationToken cancellationToken = default
	)
	{
		Queries.Add(query);
		return Error != null
			? Task.FromException<CalendarSourceResultDto>(Error)
			: Task.FromResult(Result);
	}
}
