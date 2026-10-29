using LT.Contracts.Dtos.Fundamentals;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

/// <summary>
/// SEC facts into fiscal years and quarters, shaped like Apple's companyfacts (fiscal year ending
/// late September, cash flows only as years to date in the 10-Qs, every value repeated by the
/// next year's filings).
/// </summary>
public class FinancialsHelpersTests
{
	private const string Ocf = "NetCashProvidedByUsedInOperatingActivities";
	private const string NewRevenue = "RevenueFromContractWithCustomerExcludingAssessedTax";

	[Fact]
	public void Quarters_reported_only_as_years_to_date_are_worked_out_and_q4_comes_from_the_10k()
	{
		var financials = FinancialsHelpers.Build(AppleCashFlows());

		var quarters = financials.Quarterly;
		Assert.Equal(
			["Q3 2026", "Q2 2026", "Q1 2026", "Q4 2025", "Q3 2025", "Q2 2025", "Q1 2025"],
			quarters.Select(Name)
		);
		// 9 months less 6, 6 less 3; Q1 is the 3-month year to date as reported
		Assert.Equal(116_996m - 82_627m, quarters[0].Get(FinancialItem.OperatingCashFlow));
		Assert.Equal(82_627m - 53_925m, quarters[1].Get(FinancialItem.OperatingCashFlow));
		Assert.Equal(53_925m, quarters[2].Get(FinancialItem.OperatingCashFlow));
		Assert.Contains(FinancialItem.OperatingCashFlow, quarters[0].Derived);
		Assert.DoesNotContain(FinancialItem.FreeCashFlow, quarters[0].Derived);
		Assert.DoesNotContain(FinancialItem.OperatingCashFlow, quarters[2].Derived);
		// The 10-K's year less nine months
		Assert.Equal(111_482m - 81_754m, quarters[3].Get(FinancialItem.OperatingCashFlow));
		Assert.Equal(new DateOnly(2025, 6, 29), quarters[3].Start);
		Assert.Equal(new DateOnly(2025, 9, 27), quarters[3].End);
	}

	[Fact]
	public void Fiscal_years_are_named_by_the_year_they_end_in()
	{
		var financials = FinancialsHelpers.Build(AppleCashFlows());

		Assert.Equal(["FY2025", "FY2024"], financials.Annual.Select(Name));
		Assert.Equal(111_482m, financials.Annual[0].Get(FinancialItem.OperatingCashFlow));
		Assert.Equal(new DateOnly(2024, 9, 29), financials.Annual[0].Start);
	}

	[Fact]
	public void A_value_filed_again_later_is_taken_from_the_latest_filing()
	{
		var facts = AppleCashFlows().ToList();
		facts.Add(Flow(Ocf, "2024-09-29", "2025-09-27", 110_000, "10-K", "2026-10-30"));

		var financials = FinancialsHelpers.Build(facts);

		Assert.Equal(110_000m, financials.Annual[0].Get(FinancialItem.OperatingCashFlow));
	}

	[Fact]
	public void Each_period_takes_the_first_concept_of_the_line_that_has_it()
	{
		// Apple: Revenues until FY2018, then the ASC 606 concept
		var financials = FinancialsHelpers.Build([
			Flow("Revenues", "2016-09-25", "2017-09-30", 229_234, "10-K"),
			Flow(NewRevenue, "2017-10-01", "2018-09-29", 265_595, "10-K"),
			Flow("Revenues", "2017-10-01", "2018-09-29", 1, "10-K"),
		]);

		Assert.Equal(["FY2018", "FY2017"], financials.Annual.Select(Name));
		// Revenues comes first in the line's list, so it wins where both are reported
		Assert.Equal(1m, financials.Annual[0].Get(FinancialItem.Revenue));
		Assert.Equal(229_234m, financials.Annual[1].Get(FinancialItem.Revenue));
	}

	[Fact]
	public void Per_share_values_are_never_worked_out_from_years_to_date()
	{
		var financials = FinancialsHelpers.Build([
			Flow(NewRevenue, "2024-09-29", "2025-06-28", 300, "10-Q"),
			Flow(NewRevenue, "2024-09-29", "2025-09-27", 400, "10-K"),
			Flow("EarningsPerShareDiluted", "2024-09-29", "2025-06-28", 5.62m, "10-Q"),
			Flow("EarningsPerShareDiluted", "2024-09-29", "2025-09-27", 7.46m, "10-K"),
			Flow("EarningsPerShareDiluted", "2025-03-30", "2025-06-28", 1.57m, "10-Q"),
		]);

		var q4 = financials.Quarterly.Single(x => x.FiscalQuarter == 4);
		Assert.Equal(100m, q4.Get(FinancialItem.Revenue));
		Assert.Null(q4.Get(FinancialItem.EpsDiluted));
		Assert.Equal(7.46m, financials.Annual[0].Get(FinancialItem.EpsDiluted));
	}

	[Fact]
	public void Balances_are_read_as_of_the_period_end_and_missing_lines_are_computed()
	{
		var financials = FinancialsHelpers.Build([
			Flow(NewRevenue, "2025-03-30", "2025-06-28", 1_000, "10-Q"),
			Flow("CostOfGoodsAndServicesSold", "2025-03-30", "2025-06-28", 540, "10-Q"),
			Flow(Ocf, "2025-03-30", "2025-06-28", 300, "10-Q"),
			Flow(
				"PaymentsToAcquirePropertyPlantAndEquipment",
				"2025-03-30",
				"2025-06-28",
				50,
				"10-Q"
			),
			Instant("LiabilitiesAndStockholdersEquity", "2025-06-28", 900),
			Instant("StockholdersEquity", "2025-06-28", 200),
			Instant("Assets", "2025-03-29", 1),
		]);

		var quarter = Assert.Single(financials.Quarterly);
		Assert.Equal(460m, quarter.Get(FinancialItem.GrossProfit));
		Assert.Equal(250m, quarter.Get(FinancialItem.FreeCashFlow));
		Assert.Equal(700m, quarter.Get(FinancialItem.TotalLiabilities));
		Assert.Null(quarter.Get(FinancialItem.TotalAssets));
		Assert.Empty(quarter.Derived);
		Assert.Equal(
			0.46,
			FinancialsHelpers.GetRatio(FinancialRatio.GrossMargin, quarter)!.Value,
			6
		);
	}

	[Fact]
	public void Free_cash_flow_of_a_worked_out_quarter_is_marked_as_worked_out()
	{
		var capex = "PaymentsToAcquirePropertyPlantAndEquipment";
		var financials = FinancialsHelpers.Build(
			AppleCashFlows()
				.Concat([
					Flow(capex, "2025-09-28", "2025-12-27", 2_373, "10-Q"),
					Flow(capex, "2025-09-28", "2026-03-28", 4_345, "10-Q"),
				])
		);

		var q2 = financials.Quarterly.Single(x => x is { FiscalYear: 2026, FiscalQuarter: 2 });
		var q1 = financials.Quarterly.Single(x => x is { FiscalYear: 2026, FiscalQuarter: 1 });
		Assert.Equal(82_627m - 53_925m - (4_345m - 2_373m), q2.Get(FinancialItem.FreeCashFlow));
		Assert.Contains(FinancialItem.FreeCashFlow, q2.Derived);
		Assert.DoesNotContain(FinancialItem.FreeCashFlow, q1.Derived);
	}

	[Fact]
	public void Only_10k_and_10q_values_count()
	{
		// A foreign issuer: its 20-F values are ordinary shares' and IFRS
		var financials = FinancialsHelpers.Build(
			[Flow(NewRevenue, "2025-01-01", "2025-12-31", 100, "20-F")],
			"Taiwan Semiconductor"
		);

		Assert.Equal(FinancialsStatus.NotFiled, financials.Status);
		Assert.Empty(financials.Annual);
	}

	[Fact]
	public void The_trailing_twelve_months_add_up_four_consecutive_quarters()
	{
		var financials = FinancialsHelpers.Build(AppleCashFlows());

		Assert.Equal(
			116_996m - 81_754m + 111_482m,
			financials.TrailingTwelveMonths[FinancialItem.OperatingCashFlow]
		);
	}

	[Fact]
	public void Without_four_consecutive_quarters_there_are_no_trailing_twelve_months()
	{
		var financials = FinancialsHelpers.Build(
			AppleCashFlows().Where(x => x.End != new DateOnly(2025, 12, 27))
		);

		Assert.Empty(financials.TrailingTwelveMonths);
	}

	[Theory]
	[InlineData("2026-06-27", 2026, 3)]
	[InlineData("2025-12-27", 2026, 1)]
	[InlineData("2025-09-27", 2025, 4)]
	[InlineData("2027-03-27", 2027, 2)]
	public void Quarters_count_back_from_the_fiscal_year_end(string end, int year, int quarter)
	{
		var yearEnds = new[] { new DateOnly(2024, 9, 28), new DateOnly(2025, 9, 27) };

		Assert.Equal(
			(year, quarter),
			FinancialsHelpers.GetFiscalQuarter(DateOnly.Parse(end), yearEnds)
		);
	}

	[Fact]
	public void Without_a_known_year_end_quarters_are_calendar_quarters()
	{
		Assert.Equal((2026, 2), FinancialsHelpers.GetFiscalQuarter(new DateOnly(2026, 6, 30), []));
	}

	[Theory]
	[InlineData("2022-01-01", 2021)]
	[InlineData("2026-01-31", 2026)]
	[InlineData("2025-09-27", 2025)]
	public void A_year_ending_in_the_first_days_of_january_is_the_previous_years(
		string end,
		int year
	)
	{
		Assert.Equal(year, FinancialsHelpers.GetFiscalYear(DateOnly.Parse(end)));
	}

	[Fact]
	public void Multiples_need_a_positive_amount()
	{
		Assert.Equal(20, FinancialsHelpers.GetMultiple(2_000, 100));
		Assert.Null(FinancialsHelpers.GetMultiple(2_000, -100));
		Assert.Null(FinancialsHelpers.GetMultiple(null, 100));
	}

	private static string Name(FinancialPeriodDto period) =>
		period.FiscalQuarter is { } quarter
			? $"Q{quarter} {period.FiscalYear}"
			: $"FY{period.FiscalYear}";

	// Apple's operating cash flow (millions) as companyfacts has it: years to date only
	private static IEnumerable<SecFactDto> AppleCashFlows() =>
		[
			Flow(Ocf, "2023-10-01", "2024-09-28", 118_254, "10-K", "2024-11-01"),
			Flow(Ocf, "2023-10-01", "2024-09-28", 118_254, "10-K", "2025-10-31"),
			Flow(Ocf, "2024-09-29", "2024-12-28", 29_935, "10-Q"),
			Flow(Ocf, "2024-09-29", "2025-03-29", 53_887, "10-Q"),
			Flow(Ocf, "2024-09-29", "2025-06-28", 81_754, "10-Q"),
			Flow(Ocf, "2024-09-29", "2025-09-27", 111_482, "10-K"),
			Flow(Ocf, "2025-09-28", "2025-12-27", 53_925, "10-Q"),
			Flow(Ocf, "2025-09-28", "2026-03-28", 82_627, "10-Q"),
			Flow(Ocf, "2025-09-28", "2026-06-27", 116_996, "10-Q"),
		];

	private static SecFactDto Flow(
		string concept,
		string start,
		string end,
		decimal value,
		string form,
		string? filed = null
	) =>
		new()
		{
			Concept = concept,
			Start = DateOnly.Parse(start),
			End = DateOnly.Parse(end),
			Filed = DateOnly.Parse(filed ?? end).AddDays(filed == null ? 35 : 0),
			Value = value,
			Form = form,
		};

	private static SecFactDto Instant(string concept, string end, decimal value) =>
		new()
		{
			Concept = concept,
			End = DateOnly.Parse(end),
			Filed = DateOnly.Parse(end).AddDays(35),
			Value = value,
			Form = "10-Q",
		};
}
