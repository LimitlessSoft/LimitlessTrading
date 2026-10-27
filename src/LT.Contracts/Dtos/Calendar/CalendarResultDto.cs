using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Calendar;

/// <summary>
/// A calendar load: the days with events between <see cref="Start"/> and <see cref="End"/>, merged
/// from every source that answered (<see cref="Sources"/>), and the sources that failed
/// (<see cref="SourceErrors"/>). When no source answered, <see cref="Status"/> is not
/// <see cref="CalendarStatus.Loaded"/>, there are no days and <see cref="Message"/> says why and
/// what to do.
/// </summary>
public class CalendarResultDto
{
	public CalendarStatus Status { get; set; }
	public DateOnly Start { get; set; }
	public DateOnly End { get; set; }
	public IReadOnlyList<CalendarDayDto> Days { get; set; } = [];
	public string? Message { get; set; }
	public DateTimeOffset FetchedAt { get; set; }

	/// <summary>The sources whose events are in <see cref="Days"/>.</summary>
	public IReadOnlyList<CalendarSource> Sources { get; set; } = [];

	/// <summary>Enabled sources that failed while others answered (or all, with the status above).</summary>
	public IReadOnlyList<CalendarSourceErrorDto> SourceErrors { get; set; } = [];
}
