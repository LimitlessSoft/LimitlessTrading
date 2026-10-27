using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Enums;

namespace LT.Contracts.Interfaces.IApiClients;

/// <summary>
/// nasdaq.com's public calendar JSON (no account, no key; unofficial, see docs/architecture.md →
/// External data: Nasdaq calendar). Requests are serialized and spaced. Throws
/// <see cref="Exceptions.NasdaqServiceException"/> with a message ready to show.
/// </summary>
public interface INasdaqApiClient
{
	/// <summary>
	/// The events of one calendar on <paramref name="day"/> (New York date). Splits are the
	/// exception: Nasdaq ignores the day and answers every split it knows effective on or after
	/// it, each on its own effective date.
	/// </summary>
	Task<IReadOnlyList<CalendarEventDto>> GetCalendarAsync(
		NasdaqCalendar calendar,
		DateOnly day,
		CancellationToken cancellationToken = default
	);
}
