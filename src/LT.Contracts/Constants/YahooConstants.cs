namespace LT.Contracts.Constants;

/// <summary>
/// Yahoo Finance's earnings calendar page, read for the reported EPS and surprise Nasdaq gives
/// only from the next day on. Unofficial (no key, no account), HTML rather than an API, so it may
/// change; checked by hand on 2026-10-29.
/// </summary>
public static class YahooConstants
{
	/// <summary>{0} the day (yyyy-MM-dd), {1} the 0-based offset.</summary>
	public const string EarningsUrl =
		"https://finance.yahoo.com/calendar/earnings?day={0}&offset={1}&size=100";

	public const string DateFormat = "yyyy-MM-dd";

	public const string SourceName = "Yahoo Finance";

	/// <summary>A browser's User-Agent and Accept: without them Yahoo ignores <c>offset</c> and <c>size</c>.</summary>
	public const string UserAgent = NasdaqConstants.UserAgent;
	public const string Accept = "text/html,application/xhtml+xml";

	public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(20);

	/// <summary>Least time between two requests; Yahoo publishes no limit.</summary>
	public static readonly TimeSpan RequestSpacing = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Pages asked for per day and poll, 100 rows each, the day's largest companies first (on
	/// 2026-10-29: 298 rows, "1-100 of 298", "101-200 of 298", "201-298 of 298"). Yahoo pages
	/// only for a browser's User-Agent with an HTML Accept (else every page was the first 25); a
	/// page that doesn't start where it was asked to ends the paging.
	/// </summary>
	public const int MaxPagesPerDay = 4;

	/// <summary>Rows a full page holds (the <c>size</c> asked for).</summary>
	public const int PageSize = 100;
}
