using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
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
		var notFound = new SharesOutstandingEntity { FetchedAt = Now };

		Assert.True(MarketCapHelpers.IsFresh(found, Now + TimeSpan.FromDays(6)));
		Assert.False(MarketCapHelpers.IsFresh(found, Now + SecConstants.SharesOutstandingTtl));
		Assert.True(MarketCapHelpers.IsFresh(notFound, Now + TimeSpan.FromDays(2)));
		Assert.False(MarketCapHelpers.IsFresh(notFound, Now + SecConstants.SharesNotFoundTtl));
	}

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
