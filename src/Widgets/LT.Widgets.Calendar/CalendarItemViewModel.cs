using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Calendar;

/// <summary>A line of the calendar list: a day's header or one of its events.</summary>
public abstract record CalendarItemViewModel
{
	/// <summary>Only events can be picked; headers just group them.</summary>
	public abstract bool IsSelectable { get; }
}

/// <summary>A day: "Wed 28 Oct · Today", how many events, and a note when WSH may have more.</summary>
public sealed record CalendarDayHeaderViewModel(string Title, string CountText, string? Warning)
	: CalendarItemViewModel
{
	public override bool IsSelectable => false;

	public bool HasWarning => Warning != null;
}

/// <summary>
/// One event: time (before open / after close, a release's clock time), symbol, company (or the
/// release), its detail (EPS estimate, dividend, ratio, figures) and type (or country).
/// </summary>
public sealed record CalendarEventRowViewModel(CalendarEventDto Event, string TimeText)
	: CalendarItemViewModel
{
	public override bool IsSelectable => true;

	public bool HasTime => TimeText.Length > 0;

	public bool HasDetail => Event.Detail.Length > 0;

	/// <summary>"Earnings · EPS est. $3.43 · from Nasdaq": everything, and where it came from.</summary>
	public string ToolTipText =>
		string.Join(
			" · ",
			new[]
			{
				Event.Company,
				Event.TypeName,
				Event.Detail,
				"from " + CalendarHelpers.GetSourceName(Event.Source),
			}.Where(x => x.Length > 0)
		);
}
