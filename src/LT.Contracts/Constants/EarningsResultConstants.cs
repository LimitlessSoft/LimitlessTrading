namespace LT.Contracts.Constants;

/// <summary>
/// Earnings results (reported EPS, surprise, the 8-K) filled into the calendar's earnings rows
/// that have none: which days are looked up, and how often each source is asked again.
/// </summary>
public static class EarningsResultConstants
{
	/// <summary>
	/// Days before today whose rows without a result are looked up once per load (a report after
	/// Friday's close seen on Monday); only today is polled.
	/// </summary>
	public const int LookbackDays = 3;

	/// <summary>
	/// The calendar polls today's results between these New York times (pre-market reports from
	/// about 06:00; the last after-close ones are in well before 22:00), and not outside them.
	/// </summary>
	public static readonly TimeOnly PollStart = new(6, 0);
	public static readonly TimeOnly PollEnd = new(22, 0);

	/// <summary>Never poll more often than this, whatever a source says.</summary>
	public static readonly TimeSpan MinPollInterval = TimeSpan.FromSeconds(30);

	/// <summary>Symbols one lookup may ask for (a day has a few hundred reporters).</summary>
	public const int MaxSymbolsPerRequest = 2000;

	/// <summary>SEC EDGAR: how often the latest 8-Ks are read while results come out, and otherwise.</summary>
	public static readonly TimeSpan SecFastInterval = TimeSpan.FromSeconds(30);
	public static readonly TimeSpan SecInterval = TimeSpan.FromMinutes(2);

	/// <summary>
	/// The New York windows when most earnings releases go out: before the open and just after
	/// the close (seen 2026-10-29: CAT 06:33, MRK 06:43, LLY 07:03, MA 08:04, AMZN 16:05, INTC
	/// 16:08 accepted).
	/// </summary>
	public static readonly IReadOnlyList<(TimeOnly Start, TimeOnly End)> SecFastWindows =
	[
		(new TimeOnly(6, 0), new TimeOnly(9, 30)),
		(new TimeOnly(16, 0), new TimeOnly(16, 30)),
	];

	/// <summary>
	/// Feed pages read per poll at most (100 8-Ks each, about 400 a day): enough for the whole day
	/// on the first poll, after which one page reaches the filings already seen.
	/// </summary>
	public const int SecMaxPagesPerPoll = 6;

	public const string SecSourceName = "SEC EDGAR";

	/// <summary>Yahoo Finance: how often its page is read again for today.</summary>
	public static readonly TimeSpan YahooInterval = TimeSpan.FromMinutes(5);

	/// <summary>Nasdaq: how often its earnings calendar is read again for results.</summary>
	public static readonly TimeSpan NasdaqInterval = TimeSpan.FromMinutes(15);

	/// <summary>A past day's answer is good for this long (results there no longer change much).</summary>
	public static readonly TimeSpan PastDayTtl = TimeSpan.FromHours(1);
}
