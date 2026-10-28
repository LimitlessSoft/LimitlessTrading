using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Fundamentals;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Entities;
using LT.Contracts.Helpers;
using static LT.Tests.Fakes.FakeSecApiClient;

namespace LT.Tests.Contracts.Helpers;

public class MarketCapHelpersTests
{
	private static readonly DateOnly Today = new(2026, 10, 26);
	private static readonly DateTimeOffset Now = new(2026, 10, 26, 14, 0, 0, TimeSpan.Zero);

	[Fact]
	public void The_newest_date_wins_then_the_latest_filing()
	{
		var picked = MarketCapHelpers.PickSharesOutstanding(
			[
				Fact("2026-04-17", 100),
				Fact("2026-07-17", 200, filed: "2026-07-31"),
				Fact("2026-07-17", 210, form: "10-Q/A", filed: "2026-08-15"),
			],
			Today
		);

		Assert.Equal(210, picked?.Shares);
		Assert.Equal(new DateOnly(2026, 7, 17), picked?.AsOf);
	}

	[Fact]
	public void Foreign_filer_reports_and_old_counts_are_ignored()
	{
		// A 20-F counts ordinary shares, not ADRs; BRK's last cover-page count is from 2011
		Assert.Null(
			MarketCapHelpers.PickSharesOutstanding(
				[Fact("2026-03-31", 25_932_524_521, form: "20-F"), Fact("2011-04-29", 941_481)],
				Today
			)
		);
		Assert.Equal(
			5,
			MarketCapHelpers
				.PickSharesOutstanding(
					[Fact("2025-12-01", 5, form: "10-K"), Fact("2026-01-01", 0)],
					Today
				)
				?.Shares
		);
	}

	[Fact]
	public void A_found_count_is_fresh_for_a_week_nothing_found_for_a_few_days()
	{
		var found = new SharesOutstandingEntity { Shares = 1, FetchedAt = Now };
		var notFound = new SharesOutstandingEntity
		{
			FetchedAt = Now,
			LookupVersion = SecConstants.SharesLookupVersion,
		};

		Assert.True(MarketCapHelpers.IsFresh(found, Now + TimeSpan.FromDays(6)));
		Assert.False(MarketCapHelpers.IsFresh(found, Now + SecConstants.SharesOutstandingTtl));
		Assert.True(MarketCapHelpers.IsFresh(notFound, Now + TimeSpan.FromDays(2)));
		Assert.False(MarketCapHelpers.IsFresh(notFound, Now + SecConstants.SharesNotFoundTtl));
	}

	[Fact]
	public void Nothing_found_by_an_older_lookup_is_not_fresh()
	{
		// Cached before weighted averages and cover pages were read (no version in the file)
		var notFound = new SharesOutstandingEntity { FetchedAt = Now };
		var found = new SharesOutstandingEntity { Shares = 1, FetchedAt = Now };

		Assert.False(MarketCapHelpers.IsFresh(notFound, Now + TimeSpan.FromMinutes(1)));
		Assert.True(MarketCapHelpers.IsFresh(found, Now + TimeSpan.FromMinutes(1)));
	}

	[Fact]
	public void Weighted_averages_are_only_a_fallback_newest_quarter_basic_first()
	{
		const string diluted = SecConstants.WeightedAverageDilutedConcept;
		List<SecFactDto> facts =
		[
			Average("2026-01-01", "2026-09-30", 2_543_000_000),
			Average("2026-07-01", "2026-09-30", 2_552_000_000),
			Average("2026-07-01", "2026-09-30", 2_568_000_000, diluted),
			Average("2026-04-01", "2026-06-30", 2_600_000_000),
			Average("2015-07-01", "2015-09-30", 1_643_118),
		];

		Assert.Null(MarketCapHelpers.PickSharesOutstanding(facts, Today));
		var picked = MarketCapHelpers.PickWeightedAverageShares(facts, Today);
		Assert.Equal(2_552_000_000, picked?.Shares);
		Assert.Equal(new DateOnly(2026, 9, 30), picked?.AsOf);
		Assert.Equal(
			2_568_000_000,
			MarketCapHelpers.PickWeightedAverageShares([facts[2]], Today)?.Shares
		);
		// Old, foreign, or not a share-count average
		Assert.Null(
			MarketCapHelpers.PickWeightedAverageShares(
				[
					facts[4],
					Average("2026-01-01", "2026-06-30", 5, form: "20-F"),
					Average("2026-01-01", "2026-06-30", 5, "StockIssuedDuringPeriodShares"),
				],
				Today
			)
		);
	}

	[Fact]
	public void Berkshire_cover_page_classes_convert_at_1500_b_per_a()
	{
		List<SecFactDto> facts =
		[
			Fact("2026-07-29", 488_450, filed: "2026-08-10", shareClass: "CommonClassAMember"),
			Fact(
				"2026-07-29",
				1_408_035_161,
				filed: "2026-08-10",
				shareClass: "CommonClassBMember"
			),
			Fact("2026-04-20", 500_000, filed: "2026-05-04", shareClass: "CommonClassAMember"),
		];

		var b = MarketCapHelpers.PickFilingShares(facts, "BRK-B", Today);
		Assert.Equal(488_450L * 1500 + 1_408_035_161, b?.Shares);
		Assert.Equal(new DateOnly(2026, 7, 29), b?.AsOf);
		Assert.Equal(
			488_450 + 938_690, // 1,408,035,161 / 1,500 rounded
			MarketCapHelpers.PickFilingShares(facts, "BRK-A", Today)?.Shares
		);
	}

	[Fact]
	public void Other_cover_page_classes_add_up_and_an_undivided_count_wins()
	{
		List<SecFactDto> classes =
		[
			Fact("2026-07-21", 1_700_000_000, shareClass: "CommonClassAMember"),
			Fact("2026-07-21", 17_000_000, shareClass: "CommonClassCMember"),
		];

		Assert.Equal(1_717_000_000, MarketCapHelpers.PickFilingShares(classes, "V", Today)?.Shares);
		Assert.Equal(
			1_720_000_000,
			MarketCapHelpers
				.PickFilingShares([.. classes, Fact("2026-07-21", 1_720_000_000)], "V", Today)
				?.Shares
		);
		Assert.Null(
			MarketCapHelpers.PickFilingShares(
				[Fact("2011-04-29", 941_481, shareClass: "CommonClassAMember")],
				"BRK-B",
				Today
			)
		);
	}

	private static SecFactDto Average(
		string start,
		string end,
		long value,
		string concept = SecConstants.WeightedAverageBasicConcept,
		string form = "10-Q"
	) => Fact(end, value, form, start: start, concept: concept);

	[Fact]
	public void Market_cap_is_shares_times_last_or_close_before_the_first_trade()
	{
		Assert.Equal(
			2_000_000,
			MarketCapHelpers.Compute(10_000, new QuoteDto { Last = 200, Close = 190 })
		);
		Assert.Equal(1_900_000, MarketCapHelpers.Compute(10_000, new QuoteDto { Close = 190 }));
		Assert.Null(MarketCapHelpers.Compute(null, new QuoteDto { Last = 200 }));
		Assert.Null(MarketCapHelpers.Compute(10_000, new QuoteDto()));
		Assert.Null(MarketCapHelpers.Compute(10_000, null));
	}

	[Fact]
	public void Only_us_stocks_are_eligible_and_classes_take_a_dash()
	{
		Assert.True(MarketCapHelpers.IsEligible(Contract("AAPL", "STK", "USD")));
		Assert.False(MarketCapHelpers.IsEligible(Contract("VOW", "STK", "EUR")));
		Assert.False(MarketCapHelpers.IsEligible(Contract("ES", "FUT", "USD")));
		Assert.False(MarketCapHelpers.IsEligible(Contract("EUR", "CASH", "USD")));
		Assert.Equal("BRK-B", MarketCapHelpers.GetSecTicker("BRK B"));
		Assert.Equal("BF-B", MarketCapHelpers.GetSecTicker("bf.b"));
	}

	private static ContractDto Contract(string symbol, string type, string currency) =>
		new()
		{
			Symbol = symbol,
			SecurityType = type,
			Currency = currency,
		};
}
