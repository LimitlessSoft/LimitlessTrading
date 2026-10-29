using LT.Contracts.Constants;
using LT.Contracts.Dtos.Calendar;
using LT.Domain.EarningsResultSources;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.EarningsResultSources;

public class SecEarningsResultSourceTests
{
	private static readonly DateOnly Thursday = new(2026, 10, 29);
	private static readonly TimeSpan NewYork = TimeSpan.FromHours(-4);

	// 16:10 New York, Thursday 29 October
	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 29, 20, 10, 0, TimeSpan.Zero)
	);
	private readonly FakeSecApiClient _sec = new();
	private readonly SecEarningsResultSource _source;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public SecEarningsResultSourceTests()
	{
		_sec.Ciks["AMZN"] = 1018724;
		_sec.Ciks["GOOGL"] = 1652044;
		_sec.Ciks["GOOG"] = 1652044;
		_sec.Ciks["ACXP"] = 1736243;
		_source = new(_sec, _time);
	}

	private static SecCurrentFilingDto Filing(
		int cik,
		string accession,
		DateTimeOffset accepted,
		params string[] items
	) =>
		new()
		{
			Cik = cik,
			Form = "8-K",
			AccessionNumber = accession,
			AcceptedAt = accepted,
			Items = items,
			Url = $"https://www.sec.gov/Archives/edgar/data/{cik}/{accession}-index.htm",
		};

	private static DateTimeOffset At(int day, int hour, int minute) =>
		new(2026, 10, day, hour, minute, 0, NewYork);

	private Task<IReadOnlyList<EarningsResultDto>> AskAsync(DateOnly? day = null) =>
		_source.GetResultsAsync(
			new EarningsResultQueryDto { Date = day ?? Thursday, Symbols = ["AMZN"] },
			Ct
		);

	[Fact]
	public async Task A_results_8k_marks_every_ticker_of_the_company_as_reported_with_its_link()
	{
		_sec.CurrentFilings.AddRange([
			Filing(1736243, "a3", At(29, 16, 9), "5.03", "9.01"),
			Filing(1018724, "a2", At(29, 16, 5), "2.02", "9.01"),
			Filing(1652044, "a1", At(29, 16, 2), "2.02", "9.01"),
			Filing(1018724, "old", At(28, 16, 1), "2.02"),
		]);

		var results = await AskAsync();

		Assert.Equal(["AMZN", "GOOG", "GOOGL"], results.Select(x => x.Symbol).Order());
		var amazon = results.Single(x => x.Symbol == "AMZN");
		Assert.Null(amazon.EpsActual);
		Assert.Equal(At(29, 16, 5), amazon.ReportedAt);
		Assert.EndsWith("a2-index.htm", amazon.FilingUrl);
		Assert.Equal(EarningsResultConstants.SecSourceName, amazon.Source);
		Assert.Equal(Thursday, amazon.Date);
	}

	[Fact]
	public async Task Pages_back_to_the_start_of_the_day_then_reads_only_whats_new()
	{
		// 250 filings today, the oldest Amazon's
		for (var i = 0; i < 250; i++)
			_sec.CurrentFilings.Add(
				Filing(9000 + i, $"f{i}", At(29, 16, 9).AddSeconds(-i), "8.01")
			);
		_sec.CurrentFilings.Add(Filing(1018724, "amzn", At(29, 6, 0), "2.02"));
		_sec.CurrentFilings.Add(Filing(1, "yesterday", At(28, 20, 0), "2.02"));

		var first = await AskAsync();
		Assert.Equal([0, 100, 200], _sec.CurrentFilingsStarts);
		Assert.Single(first);

		// The next poll, after the interval: the newest page reaches filings already seen
		_time.Advance(EarningsResultConstants.SecFastInterval);
		_sec.CurrentFilingsStarts.Clear();
		await AskAsync();
		Assert.Equal([0], _sec.CurrentFilingsStarts);
	}

	[Fact]
	public async Task A_filing_listed_twice_in_one_read_doesnt_stop_the_paging()
	{
		// EDGAR lists a filing once per filer: the same accession twice on the first page
		_sec.CurrentFilings.Add(Filing(9000, "both", At(29, 16, 9), "8.01"));
		_sec.CurrentFilings.Add(Filing(9001, "both", At(29, 16, 9), "8.01"));
		for (var i = 0; i < 150; i++)
			_sec.CurrentFilings.Add(
				Filing(9100 + i, $"f{i}", At(29, 16, 8).AddSeconds(-i), "8.01")
			);
		_sec.CurrentFilings.Add(Filing(1018724, "amzn", At(29, 16, 5).AddHours(-10), "2.02"));

		var results = await AskAsync();

		Assert.Equal([0, 100], _sec.CurrentFilingsStarts);
		Assert.Equal("AMZN", Assert.Single(results).Symbol);
	}

	[Fact]
	public async Task Is_read_again_only_once_its_interval_passed()
	{
		await AskAsync();
		_time.Advance(TimeSpan.FromSeconds(10));
		await AskAsync();

		Assert.Single(_sec.CurrentFilingsStarts);
		Assert.Equal(1, _sec.TickerCalls);
	}

	[Fact]
	public async Task Other_days_are_not_asked_for()
	{
		Assert.Empty(await AskAsync(Thursday.AddDays(-1)));
		Assert.Empty(_sec.CurrentFilingsStarts);
	}
}
