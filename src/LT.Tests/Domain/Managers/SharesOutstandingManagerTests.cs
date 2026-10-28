using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Entities;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;
using static LT.Tests.Fakes.FakeSecApiClient;

namespace LT.Tests.Domain.Managers;

public class SharesOutstandingManagerTests
{
	private const int AppleCik = 320193;
	private const int AlphabetCik = 1652044;
	private const int MetaCik = 1326801;
	private const int BerkshireCik = 1067983;

	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 26, 14, 0, 0, TimeSpan.Zero)
	);
	private readonly FakeSecApiClient _sec = new();
	private readonly InMemorySharesOutstandingRepository _repository = new();
	private readonly MessageLogManager _log = new();
	private readonly SharesOutstandingManager _manager;

	public SharesOutstandingManagerTests()
	{
		_manager = new SharesOutstandingManager(_sec, _repository, _log, _time);
		_sec.Ciks["AAPL"] = AppleCik;
		_sec.Ciks["GOOGL"] = AlphabetCik;
		_sec.Ciks["SPY"] = 884394;
		_sec.Ciks["META"] = MetaCik;
		_sec.Ciks["BRK-A"] = BerkshireCik;
		_sec.Ciks["BRK-B"] = BerkshireCik;
		_sec.ConceptFacts[AppleCik] =
		[
			Fact("2026-04-17", 14_687_356_000),
			Fact("2026-07-17", 14_594_180_000),
		];
	}

	private static ContractDto Stock(string symbol, string currency = "USD") =>
		new()
		{
			Symbol = symbol,
			SecurityType = MarketDataConstants.StockType,
			Currency = currency,
		};

	private Task<long?> GetAsync(string symbol, string currency = "USD") =>
		_manager.GetSharesOutstandingAsync(
			Stock(symbol, currency),
			TestContext.Current.CancellationToken
		);

	[Fact]
	public async Task Looks_up_the_latest_count_once_then_serves_it_from_the_cache()
	{
		Assert.Equal(14_594_180_000, await GetAsync("AAPL"));
		_time.Advance(TimeSpan.FromDays(6));
		Assert.Equal(14_594_180_000, await GetAsync("AAPL"));

		Assert.Equal(1, _sec.ConceptCalls);
		Assert.Equal(new DateOnly(2026, 7, 17), _repository.Get("AAPL")?.AsOf);
	}

	[Fact]
	public async Task A_week_old_count_is_looked_up_again()
	{
		await GetAsync("AAPL");
		_sec.ConceptFacts[AppleCik].Add(Fact("2026-10-16", 14_500_000_000));
		_time.Advance(SecConstants.SharesOutstandingTtl);

		Assert.Equal(14_500_000_000, await GetAsync("AAPL"));
		Assert.Equal(2, _sec.ConceptCalls);
	}

	[Fact]
	public async Task Nothing_usable_is_cached_for_a_few_days()
	{
		Assert.Null(await GetAsync("SPY")); // no concept, no facts
		Assert.Null(await GetAsync("ZZZZ")); // not an SEC ticker
		_time.Advance(TimeSpan.FromDays(2));
		Assert.Null(await GetAsync("SPY"));
		Assert.Null(await GetAsync("ZZZZ"));

		Assert.Equal(1, _sec.ConceptCalls);
		Assert.Equal(1, _sec.CompanyFactsCalls);

		_time.Advance(SecConstants.SharesNotFoundTtl);
		await GetAsync("SPY");
		Assert.Equal(2, _sec.ConceptCalls);
	}

	[Fact]
	public async Task Falls_back_to_company_facts_when_the_concept_has_nothing()
	{
		_sec.CompanyFacts[AlphabetCik] = [Fact("2026-06-30", 12_230_000_000)];

		Assert.Equal(12_230_000_000, await GetAsync("GOOGL"));
	}

	[Fact]
	public async Task Falls_back_to_the_weighted_average_when_no_count_is_usable()
	{
		// META tags its cover-page counts per class only, which the JSON APIs leave out
		_sec.CompanyFacts[MetaCik] =
		[
			Fact(
				"2026-09-30",
				2_552_000_000,
				start: "2026-07-01",
				concept: SecConstants.WeightedAverageBasicConcept
			),
		];

		Assert.Equal(2_552_000_000, await GetAsync("META"));
		Assert.Equal(0, _sec.FilingCalls);
	}

	[Fact]
	public async Task Falls_back_to_the_latest_filing_cover_page_in_the_ticker_class()
	{
		_sec.ConceptFacts[BerkshireCik] = [Fact("2011-04-29", 941_481)];
		_sec.FilingFacts[BerkshireCik] =
		[
			Fact("2026-07-29", 488_450, shareClass: "CommonClassAMember"),
			Fact("2026-07-29", 1_408_035_161, shareClass: "CommonClassBMember"),
		];

		Assert.Equal(2_140_710_161, await GetAsync("BRK B"));
		Assert.Equal(1_427_140, await GetAsync("BRK A"));
		Assert.Equal(new DateOnly(2026, 7, 29), _repository.Get("BRK-B")?.AsOf);
	}

	[Fact]
	public async Task Nothing_found_by_an_older_lookup_is_looked_up_again_at_once()
	{
		// What the cache file holds from before: no version, fetched an hour ago
		_repository.Save(
			"META",
			new SharesOutstandingEntity { FetchedAt = _time.GetUtcNow() - TimeSpan.FromHours(1) }
		);
		_sec.CompanyFacts[MetaCik] =
		[
			Fact(
				"2026-09-30",
				2_552_000_000,
				start: "2026-07-01",
				concept: SecConstants.WeightedAverageBasicConcept
			),
		];

		Assert.Equal(2_552_000_000, await GetAsync("META"));
		Assert.Equal(SecConstants.SharesLookupVersion, _repository.Get("META")?.LookupVersion);
	}

	[Fact]
	public async Task Non_us_stocks_are_never_looked_up()
	{
		Assert.Null(await GetAsync("AAPL", "EUR"));
		Assert.Equal(0, _sec.TickerCalls);
	}

	[Fact]
	public async Task Concurrent_lookups_of_a_ticker_share_one_request()
	{
		_sec.Gate = new TaskCompletionSource();
		var lookups = Enumerable.Range(0, 5).Select(_ => GetAsync("AAPL")).ToList();
		await Task.Delay(50, TestContext.Current.CancellationToken);
		_sec.Gate.SetResult();

		var results = await Task.WhenAll(lookups);

		Assert.All(results, x => Assert.Equal(14_594_180_000, x));
		Assert.Equal(1, _sec.ConceptCalls);
		Assert.Equal(1, _sec.TickerCalls);
	}

	[Fact]
	public async Task A_failure_keeps_the_stale_count_backs_off_and_is_logged_once()
	{
		await GetAsync("AAPL");
		_time.Advance(SecConstants.SharesOutstandingTtl);
		_sec.Failure = new HttpRequestException("SEC down");

		Assert.Equal(14_594_180_000, await GetAsync("AAPL"));
		Assert.Null(await GetAsync("GOOGL"));
		Assert.Equal(14_594_180_000, await GetAsync("AAPL"));

		Assert.Equal(2, _sec.ConceptCalls); // the first lookup, then the failed one; no more
		Assert.Single(_log.GetAll(), x => x.Source == MessageLogConstants.SourceSec);

		_sec.Failure = null;
		_time.Advance(SecConstants.FailureBackoff);
		Assert.Equal(14_594_180_000, await GetAsync("AAPL"));
		Assert.Equal(3, _sec.ConceptCalls);
	}
}
