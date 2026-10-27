using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Enums;

namespace LT.Contracts.Interfaces.ICalendarSources;

/// <summary>
/// Somewhere calendar events come from (IB's Wall Street Horizon, Nasdaq). <c>ICalendarManager</c>
/// asks every source switched on in Settings and merges their events; one failing source doesn't
/// hide the others'. A source keeps its own answers in memory and paces its own requests.
/// </summary>
public interface ICalendarSource
{
	CalendarSource Id { get; }

	/// <summary>Shown in the status line and the event tooltip ("Nasdaq").</summary>
	string Name { get; }

	/// <summary>Whether it can be asked now (WSH: connected to IB).</summary>
	bool IsAvailable { get; }

	/// <summary>Why it can't be asked while <see cref="IsAvailable"/> is false.</summary>
	string UnavailableMessage { get; }

	/// <summary>
	/// The events of the query's days that pass its filter. Refusals and answers that can't be
	/// read come back as a status and a message; only cancellation and unexpected errors throw.
	/// </summary>
	Task<CalendarSourceResultDto> GetEventsAsync(
		CalendarSourceQueryDto query,
		CancellationToken cancellationToken = default
	);
}
