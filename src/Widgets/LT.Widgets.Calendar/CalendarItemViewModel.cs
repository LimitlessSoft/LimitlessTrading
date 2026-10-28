using CommunityToolkit.Mvvm.ComponentModel;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Calendar;

/// <summary>A line of the calendar list: a day's header, one of its events, or the "now" line.</summary>
public abstract class CalendarItemViewModel : ObservableObject
{
	/// <summary>Only events can be picked; headers and the "now" line just structure the list.</summary>
	public abstract bool IsSelectable { get; }
}

/// <summary>
/// A day: "Wed 28 Oct · Today", how many events, and a note when WSH may have more. Today's
/// header stands out; days before today are dimmed like their events.
/// </summary>
public sealed class CalendarDayHeaderViewModel(
	DateOnly date,
	string title,
	string countText,
	string? warning,
	bool isToday,
	bool isPast
) : CalendarItemViewModel
{
	public override bool IsSelectable => false;

	public DateOnly Date { get; } = date;

	public string Title { get; } = title;

	public string CountText { get; } = countText;

	public string? Warning { get; } = warning;

	public bool HasWarning => Warning != null;

	public bool IsToday { get; } = isToday;

	public bool IsPast { get; } = isPast;
}

/// <summary>
/// Where the present falls in a range that holds today: after the events already past, before
/// the first one still to come. Moves down the list as events pass.
/// </summary>
public sealed class CalendarNowMarkerViewModel : CalendarItemViewModel
{
	public override bool IsSelectable => false;
}

/// <summary>
/// One event: a release's country flag, time (before open / after close, a release's clock
/// time), symbol, company (or the release), its detail (EPS estimate, dividend, ratio, figures)
/// and type (or a release's country when no flag is drawn for it). Dimmed once past
/// (<see cref="CalendarHelpers.GetPassedAt"/>).
/// </summary>
public sealed partial class CalendarEventRowViewModel : CalendarItemViewModel
{
	public CalendarEventRowViewModel(
		CalendarEventDto item,
		string timeText,
		DateTimeOffset passedAt,
		bool isPast
	)
	{
		Event = item;
		TimeText = timeText;
		PassedAt = passedAt;
		IsPast = isPast;
		// An economic release names its country in the type
		FlagCode =
			item.Kind == CalendarEventKind.Economic
				? CountryFlagHelpers.GetCode(item.TypeName)
				: null;
	}

	public override bool IsSelectable => true;

	public CalendarEventDto Event { get; }

	public string TimeText { get; }

	/// <summary>When the event turns past.</summary>
	public DateTimeOffset PassedAt { get; }

	/// <summary>Already happened (a release out, an earnings report in): drawn dimmed.</summary>
	[ObservableProperty]
	public partial bool IsPast { get; set; }

	/// <summary>The release's country flag (<c>US</c>, <c>EU</c> …); null for corporate events and unknown countries.</summary>
	public string? FlagCode { get; }

	public bool HasFlag => FlagCode != null;

	/// <summary>The type column: the event type, or the country of a release without a flag.</summary>
	public bool ShowsTypeName => !HasFlag && Event.TypeName.Length > 0;

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
