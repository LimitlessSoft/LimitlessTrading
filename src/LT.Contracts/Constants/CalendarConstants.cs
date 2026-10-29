namespace LT.Contracts.Constants;

public static class CalendarConstants
{
	/// <summary>The sources' names in the status line, Settings and the event tooltip.</summary>
	public const string WallStreetHorizonSourceName = "IB Wall Street Horizon";
	public const string NasdaqSourceName = "Nasdaq";

	/// <summary>IB's market data subscription for corporate events, as IB names it.</summary>
	public const string SubscriptionName = "Wall Street Horizon Corporate Event Data";

	/// <summary>
	/// "News feed is not allowed.": IB's answer to every WSH request (metadata and events) on an
	/// account without the subscription (recorded read-only on the live account, 2026-10-26).
	/// </summary>
	public static readonly IReadOnlyList<int> NotSubscribedErrorCodes = [10276];

	/// <summary>
	/// Events asked for per request (the filter's <c>limit</c> and the total limit). IB's
	/// documentation caps WSH results at 100 per request; not verified on the wire (no
	/// subscription). The calendar asks for one day per request, so a day showing this many
	/// events may have more.
	/// </summary>
	public const int MaxEventsPerRequest = 100;

	/// <summary>
	/// The longest range one load covers (a month). WSH: one event request per day, with the
	/// metadata request at most 32, one after another through the message throttle. Nasdaq: see
	/// <see cref="NasdaqConstants"/>.
	/// </summary>
	public const int MaxDaysPerLoad = 31;

	/// <summary>Market-wide: WSH's <c>country</c> filter value for every country.</summary>
	public const string AllCountries = "All";

	/// <summary>WSH's tag of earnings dates (IB's documentation and examples).</summary>
	public const string EarningsTag = "wshe_ed";

	/// <summary>WSH's tag of board of directors meetings (IB's documentation and examples).</summary>
	public const string BoardMeetingTag = "wshe_bod";

	/// <summary>Every event type tag starts with this.</summary>
	public const string TagPrefix = "wshe_";

	/// <summary>A day's events are answered from memory for this long, per source (Refresh asks again).</summary>
	public static readonly TimeSpan EventsTtl = TimeSpan.FromMinutes(30);

	/// <summary>
	/// When an event before the open counts as past (New York time): the regular session's open,
	/// by which a before-open report is out.
	/// </summary>
	public static readonly TimeOnly BeforeOpenPassedAt = new(9, 30);

	/// <summary>When an event during market hours counts as past: the regular session's close.</summary>
	public static readonly TimeOnly DuringMarketPassedAt = new(16, 0);

	/// <summary>
	/// When an event after the close counts as past: the end of the after-hours session, by which
	/// the after-close reports are out (they come at any time from 16:00 on).
	/// </summary>
	public static readonly TimeOnly AfterClosePassedAt = new(20, 0);

	/// <summary>How much of an unreadable answer the error message quotes.</summary>
	public const int UnreadableSnippetLength = 200;

	/// <summary>What a figure column shows for a figure the source didn't give.</summary>
	public const string NoFigure = "\u2014";
}
