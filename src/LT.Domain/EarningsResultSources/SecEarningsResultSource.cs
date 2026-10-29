using LT.Contracts.Constants;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IEarningsResultSources;

namespace LT.Domain.EarningsResultSources;

/// <summary>
/// The company's own earnings release: an 8-K with Item 2.02 (Results of Operations) on EDGAR's
/// latest filings feed, filed as the release goes out (2026-10-29: Amazon's accepted 16:05,
/// Yahoo still had no figure at 17:35). Gives no EPS, only that the report is out, when, and a
/// link to it; the figures come from the other sources. Today only: the feed is read newest
/// first, page by page until it reaches the filings already seen or yesterday (at most
/// <see cref="EarningsResultConstants.SecMaxPagesPerPoll"/> pages), every
/// <see cref="EarningsResultHelpers.GetSecInterval"/>; one request per page, through the SEC
/// client's spacing. Companies are matched by CIK through the SEC's ticker file. Registered as
/// a singleton.
/// </summary>
public class SecEarningsResultSource(ISecApiClient secApiClient, TimeProvider timeProvider)
	: IEarningsResultSource
{
	private readonly SemaphoreSlim _gate = new(1, 1);
	private readonly TimeZoneInfo _zone = TradeJournalHelpers.GetTradeDayTimeZone();
	private readonly HashSet<string> _seen = [];
	private readonly Dictionary<int, SecCurrentFilingDto> _releases = [];
	private Dictionary<int, List<string>>? _tickers;
	private DateOnly _day;
	private DateTimeOffset _fetchedAt = DateTimeOffset.MinValue;

	public string Name => EarningsResultConstants.SecSourceName;

	public TimeSpan GetRefreshInterval(DateTimeOffset now) =>
		EarningsResultHelpers.GetSecInterval(now, _zone);

	public async Task<IReadOnlyList<EarningsResultDto>> GetResultsAsync(
		EarningsResultQueryDto query,
		CancellationToken cancellationToken = default
	)
	{
		var now = timeProvider.GetUtcNow();
		if (query.Date != CalendarHelpers.GetToday(now))
			return [];
		await _gate.WaitAsync(cancellationToken);
		try
		{
			if (_day != query.Date)
			{
				_day = query.Date;
				_seen.Clear();
				_releases.Clear();
				_fetchedAt = DateTimeOffset.MinValue;
			}
			_tickers ??= GroupByCik(await secApiClient.GetTickerCiksAsync(cancellationToken));
			if (query.Refresh || now - _fetchedAt >= GetRefreshInterval(now))
			{
				await ReadFeedAsync(cancellationToken);
				_fetchedAt = timeProvider.GetUtcNow();
			}
			return
			[
				.. _releases.Values.SelectMany(filing =>
					_tickers
						.GetValueOrDefault(filing.Cik, [])
						.Select(symbol => new EarningsResultDto
						{
							Symbol = symbol,
							Date = _day,
							ReportedAt = filing.AcceptedAt,
							Source = Name,
							FilingUrl = filing.Url,
						})
				),
			];
		}
		finally
		{
			_gate.Release();
		}
	}

	// Newest first; stops at a filing seen by an earlier poll, one from before today or a short
	// page. A filing can be listed more than once in one read (an entry per filer, pages shifting
	// as new ones arrive), which doesn't stop it
	private async Task ReadFeedAsync(CancellationToken cancellationToken)
	{
		var seenBefore = _seen.ToHashSet();
		for (var page = 0; page < EarningsResultConstants.SecMaxPagesPerPoll; page++)
		{
			var filings = await secApiClient.GetCurrentFilingsAsync(
				SecConstants.CurrentReportForm,
				page * SecConstants.CurrentFilingsPageSize,
				cancellationToken
			);
			var reachedKnown = false;
			foreach (var filing in filings)
			{
				if (TradeJournalHelpers.GetTradeDay(filing.AcceptedAt, _zone) < _day)
				{
					reachedKnown = true;
					break;
				}
				if (seenBefore.Contains(filing.AccessionNumber))
				{
					reachedKnown = true;
					continue;
				}
				_seen.Add(filing.AccessionNumber);
				if (
					!filing.Form.StartsWith(
						SecConstants.CurrentReportForm,
						StringComparison.Ordinal
					) || !filing.Items.Contains(SecConstants.ResultsOfOperationsItem)
				)
					continue;
				// The first release of the day counts (a later 8-K/A doesn't move it)
				if (
					!_releases.TryGetValue(filing.Cik, out var kept)
					|| filing.AcceptedAt < kept.AcceptedAt
				)
					_releases[filing.Cik] = filing;
			}
			if (reachedKnown || filings.Count < SecConstants.CurrentFilingsPageSize)
				return;
		}
	}

	private static Dictionary<int, List<string>> GroupByCik(
		IReadOnlyDictionary<string, int> ciks
	) => ciks.GroupBy(x => x.Value).ToDictionary(x => x.Key, x => x.Select(y => y.Key).ToList());
}
