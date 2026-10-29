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
/// A day: "Wed 28 Oct · Today", how many events, and a note when WSH may have more; over the
/// earnings figure columns their captions (Est. / Actual / Surprise) when the day starts with
/// earnings (else its first earnings row has them). Today's header stands out; days before
/// today are dimmed like their events.
/// </summary>
public sealed class CalendarDayHeaderViewModel(
	DateOnly date,
	string title,
	string countText,
	string? warning,
	bool isToday,
	bool isPast,
	bool showsCaptions
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

	/// <summary>Captions over the earnings figure columns: the day's first event is earnings.</summary>
	public bool ShowsCaptions { get; } = showsCaptions;
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
/// time), symbol, company (or the release), then either an earnings report's figures in three
/// columns (EPS estimate, reported EPS, surprise coloured as a beat or a miss) or the detail
/// (dividend, ratio, a release's figures) with the type (or a release's country when no flag is
/// drawn for it). Dimmed once past (<see cref="CalendarHelpers.GetPassedAt"/>), all but the
/// reported figures.
/// </summary>
public sealed partial class CalendarEventRowViewModel : CalendarItemViewModel
{
	public CalendarEventRowViewModel(
		CalendarEventDto item,
		string timeText,
		DateTimeOffset passedAt,
		bool isPast,
		bool showsCaptions = false
	)
	{
		Event = item;
		TimeText = timeText;
		PassedAt = passedAt;
		IsPast = isPast;
		ShowsCaptions = showsCaptions;
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

	/// <summary>
	/// The figure columns' captions above this row: the day's first earnings row when other
	/// events (releases, whose own figures are labelled) come before it, so the day's header
	/// doesn't caption them.
	/// </summary>
	public bool ShowsCaptions { get; }

	/// <summary>
	/// Earnings show their figures in columns: always, unless only a source's text came (WSH),
	/// which is then shown as the detail.
	/// </summary>
	public bool ShowsFigures =>
		Event.Kind == CalendarEventKind.Earnings
		&& (
			Event.EpsEstimate is not null || Event.EpsActual is not null || Event.Detail.Length == 0
		);

	/// <summary>The detail text and type, right-aligned where earnings have their figures.</summary>
	public bool ShowsDetail => !ShowsFigures;

	/// <summary>
	/// The type label: what the event is (ex-dividend, split, a meeting …), or the country of a
	/// release without a flag. Not on earnings, the calendar's main kind, which their figure
	/// columns already tell apart.
	/// </summary>
	public bool ShowsTypeName =>
		!HasFlag && Event.Kind != CalendarEventKind.Earnings && Event.TypeName.Length > 0;

	public bool HasTime => TimeText.Length > 0;

	public bool HasDetail => Event.Detail.Length > 0;

	/// <summary>The EPS estimate, or an em dash.</summary>
	public string EstimateText => CalendarHelpers.FormatEps(Event.EpsEstimate);

	/// <summary>The reported EPS; empty until reported.</summary>
	public string ActualText =>
		Event.EpsActual is null ? string.Empty : CalendarHelpers.FormatEps(Event.EpsActual);

	/// <summary>The surprise (an em dash when the source gave none); empty until reported.</summary>
	public string SurpriseText =>
		Event.EpsActual is null
			? string.Empty
			: CalendarHelpers.FormatSurprise(Event.EpsSurprisePercent);

	/// <summary>Beat the estimate: the surprise in the positive colour.</summary>
	public bool IsBeat => Event.EpsActual is not null && Event.EpsSurprisePercent > 0;

	/// <summary>Missed the estimate: the surprise in the negative colour.</summary>
	public bool IsMiss => Event.EpsActual is not null && Event.EpsSurprisePercent < 0;

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
