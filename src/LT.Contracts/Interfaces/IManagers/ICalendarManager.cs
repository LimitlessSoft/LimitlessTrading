using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Requests.Calendar;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>
/// Market-wide calendar events (earnings, dividends, splits, meetings, economic releases) merged
/// from every <c>ICalendarSource</c> switched on in Settings → Calendar: IB's Wall Street Horizon
/// and Nasdaq. One load at a time.
/// </summary>
public interface ICalendarManager
{
	/// <summary>Raised when the sources switched on in Settings change; the calendar loads again.</summary>
	event Action? SourcesChanged;

	/// <summary>
	/// The events of the request's range, grouped by day, from the sources that answered; the
	/// failed ones are listed with why. When none answered (or none is on), the result carries a
	/// status and a message instead of days. Never throws for a source's failure.
	/// </summary>
	Task<CalendarResultDto> GetEventsAsync(
		GetCalendarEventsRequest request,
		CancellationToken cancellationToken = default
	);
}
