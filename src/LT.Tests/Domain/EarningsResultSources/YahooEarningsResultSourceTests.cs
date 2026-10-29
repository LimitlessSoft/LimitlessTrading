using LT.Contracts.Constants;
using LT.Contracts.Dtos.Calendar;
using LT.Domain.EarningsResultSources;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.EarningsResultSources;

public class YahooEarningsResultSourceTests
{
	private static readonly DateOnly Thursday = new(2026, 10, 29);

	// 17:30 New York, Thursday 29 October
	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 29, 21, 30, 0, TimeSpan.Zero)
	);
	private readonly FakeYahooApiClient _yahoo = new();
	private readonly YahooEarningsResultSource _source;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public YahooEarningsResultSourceTests() => _source = new(_yahoo, _time);

	private static (string, decimal?, decimal?)[] Rows(int from, int count) =>
		[.. Enumerable.Range(from, count).Select(x => ($"S{x}", (decimal?)x, (decimal?)null))];

	private Task<IReadOnlyList<EarningsResultDto>> AskAsync(DateOnly? day = null) =>
		_source.GetResultsAsync(
			new EarningsResultQueryDto { Date = day ?? Thursday, Symbols = ["LLY"] },
			Ct
		);

	[Fact]
	public async Task Reads_every_page_of_the_day_and_keeps_only_rows_with_figures()
	{
		_yahoo.Answer = (day, offset) =>
			offset switch
			{
				0 => FakeYahooApiClient.Page(day, 1, 150, Rows(1, 100)),
				100 => FakeYahooApiClient.Page(
					day,
					101,
					150,
					[("LLY", 7.48m, 17.88m), ("AMZN", null, null), .. Rows(103, 48)]
				),
				_ => throw new InvalidOperationException("past the last page"),
			};

		var results = await AskAsync();

		Assert.Equal([(Thursday, 0), (Thursday, 100)], _yahoo.Requests);
		Assert.Equal(149, results.Count);
		Assert.Contains(results, x => x.Symbol == "LLY" && x.EpsActual == 7.48m);
		Assert.DoesNotContain(results, x => x.Symbol == "AMZN");
	}

	[Fact]
	public async Task A_page_that_ignores_the_offset_ends_the_paging()
	{
		_yahoo.Answer = (day, _) => FakeYahooApiClient.Page(day, 1, 298, Rows(1, 100));

		var results = await AskAsync();

		Assert.Equal(2, _yahoo.Requests.Count);
		Assert.Equal(100, results.Count);
	}

	[Fact]
	public async Task Today_is_read_again_only_once_its_interval_passed()
	{
		_yahoo.Answer = (day, _) => FakeYahooApiClient.Page(day, 1, 1, ("LLY", 7.48m, 17.88m));

		await AskAsync();
		_time.Advance(EarningsResultConstants.YahooInterval - TimeSpan.FromSeconds(1));
		await AskAsync();
		Assert.Single(_yahoo.Requests);

		_time.Advance(TimeSpan.FromSeconds(1));
		await AskAsync();
		Assert.Equal(2, _yahoo.Requests.Count);
	}

	[Fact]
	public async Task Later_days_have_no_results_and_are_not_asked_for()
	{
		var results = await AskAsync(Thursday.AddDays(1));

		Assert.Empty(results);
		Assert.Empty(_yahoo.Requests);
	}
}
