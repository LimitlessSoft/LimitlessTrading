namespace LT.Contracts.Constants;

/// <summary>
/// nasdaq.com's calendars: the public JSON endpoints behind its Earnings, Dividends, Splits and
/// Economic calendar pages. Unofficial and undocumented (no key, no account), so they may change
/// or go away; checked by hand on 2026-10-27.
/// </summary>
public static class NasdaqConstants
{
	public const string BaseUrl = "https://api.nasdaq.com/api/calendar/";
	public const string EarningsPath = "earnings";
	public const string DividendsPath = "dividends";
	public const string SplitsPath = "splits";
	public const string EconomicPath = "economicevents";

	/// <summary>The <c>date</c> query parameter's format.</summary>
	public const string DateFormat = "yyyy-MM-dd";

	/// <summary>
	/// Nasdaq doesn't answer without a browser's User-Agent (the request hangs until it times out),
	/// so the client sends one, with a browser's Accept.
	/// </summary>
	public const string UserAgent =
		"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36";
	public const string Accept = "application/json, text/plain, */*";

	public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(20);

	/// <summary>
	/// Least time between two requests (one at a time anyway). Nasdaq publishes no limit; a month
	/// is at most ~80 requests, kept polite.
	/// </summary>
	public static readonly TimeSpan RequestSpacing = TimeSpan.FromMilliseconds(250);

	/// <summary>
	/// The economic calendar is a day behind its <c>date</c>: <c>date=2026-10-27</c> answers the
	/// releases of Monday 26 October (Chicago Fed, Dallas Fed, New Zealand's Labour Day), so the
	/// day after the one wanted is asked for (seen 2026-10-27 across five dates).
	/// </summary>
	public const int EconomicDateOffsetDays = 1;

	/// <summary>Nasdaq's "No record found." business code: an empty day, not an error.</summary>
	public const int NoRecordCode = 1002;

	/// <summary>
	/// The calendars never asked for on Saturdays and Sundays: they had no rows on the weekend
	/// days tried. Economic releases (and holidays) happen on weekends, so they are asked for.
	/// </summary>
	public static readonly IReadOnlyList<Enums.NasdaqCalendar> WeekdayOnlyCalendars =
	[
		Enums.NasdaqCalendar.Earnings,
		Enums.NasdaqCalendar.Dividends,
	];
}
