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
/// reported figures. An earnings row its source gave no result for takes one from
/// <see cref="Result"/> (another source's, filled in while the widget is open); with only the
/// company's 8-K known, the Actual column links to it.
/// </summary>
public sealed partial class CalendarEventRowViewModel : CalendarItemViewModel
{
	public CalendarEventRowViewModel(
		CalendarEventDto item,
		string timeText,
		DateTimeOffset passedAt,
		bool isPast,
		bool showsCaptions = false,
		Func<DateTimeOffset, string>? formatTime = null
	)
	{
		Event = item;
		_formatTime =
			formatTime
			?? (x => x.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture));
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

	private readonly Func<DateTimeOffset, string> _formatTime;

	public override bool IsSelectable => true;

	public CalendarEventDto Event { get; }

	/// <summary>
	/// The result found for this row by the earnings result sources (its own source had none);
	/// null until one is.
	/// </summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(
		nameof(ShowsFigures),
		nameof(ShowsDetail),
		nameof(ActualText),
		nameof(HasActualText),
		nameof(SurpriseText),
		nameof(IsBeat),
		nameof(IsMiss),
		nameof(FilingUrl),
		nameof(ShowsFilingLink),
		nameof(ToolTipText)
	)]
	public partial EarningsResultDto? Result { get; set; }

	/// <summary>The reported EPS: the row's source's, else the result's.</summary>
	public decimal? EpsActual => Event.EpsActual ?? Result?.EpsActual;

	/// <summary>The surprise to the reported EPS (worked out from the estimate when a source gave none).</summary>
	public decimal? EpsSurprisePercent =>
		Event.EpsActual is not null
			? Event.EpsSurprisePercent
			: EarningsResultHelpers.GetSurprisePercent(Event.EpsEstimate, Result);

	/// <summary>The company's earnings release on EDGAR, when its 8-K was found.</summary>
	public string? FilingUrl => Result?.FilingUrl;

	/// <summary>Reported (the 8-K is out) but no figure yet: the Actual column links to the filing.</summary>
	public bool ShowsFilingLink => EpsActual is null && FilingUrl != null;

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
			Event.EpsEstimate is not null
			|| EpsActual is not null
			|| Result != null
			|| Event.Detail.Length == 0
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
		EpsActual is null ? string.Empty : CalendarHelpers.FormatEps(EpsActual);

	public bool HasActualText => !ShowsFilingLink;

	/// <summary>The surprise (an em dash when the source gave none); empty until reported.</summary>
	public string SurpriseText =>
		EpsActual is null ? string.Empty : CalendarHelpers.FormatSurprise(EpsSurprisePercent);

	/// <summary>Beat the estimate: the surprise in the positive colour.</summary>
	public bool IsBeat => EpsActual is not null && EpsSurprisePercent > 0;

	/// <summary>Missed the estimate: the surprise in the negative colour.</summary>
	public bool IsMiss => EpsActual is not null && EpsSurprisePercent < 0;

	/// <summary>
	/// "Earnings · EPS $7.48 · est. $6.35 · +17.9% · from Nasdaq · EPS from Yahoo Finance · 8-K
	/// filed 07:03 (SEC EDGAR)": everything, and where each part came from.
	/// </summary>
	public string ToolTipText =>
		string.Join(
			" · ",
			new[]
			{
				Event.Company,
				Event.TypeName,
				Result?.HasFigures == true && Event.EpsActual is null
					? CalendarHelpers.FormatEarningsDetail(
						Event.EpsEstimate,
						EpsActual,
						EpsSurprisePercent
					)
					: Event.Detail,
				"from " + CalendarHelpers.GetSourceName(Event.Source),
				EarningsResultHelpers.Describe(Result, _formatTime),
			}.Where(x => x.Length > 0)
		);
}
