using LT.Contracts.Dtos.Fundamentals;
using LT.Contracts.Enums;

namespace LT.Contracts.Constants;

/// <summary>
/// The Overview widget's Financials tab: the statement lines read from SEC filings, how periods
/// are recognised and how long a lookup is kept.
/// </summary>
public static class FinancialsConstants
{
	/// <summary>Fiscal years shown (the SEC's facts go back to 2009).</summary>
	public const int AnnualPeriods = 6;

	/// <summary>Quarters shown.</summary>
	public const int QuarterlyPeriods = 8;

	/// <summary>A period this long (in days, both ends counted) is a quarter: 13 weeks, 3 months or a 14-week quarter.</summary>
	public const int MinQuarterDays = 80;
	public const int MaxQuarterDays = 100;

	/// <summary>A period this long is a fiscal year: 52 / 53 weeks or 12 months.</summary>
	public const int MinYearDays = 350;
	public const int MaxYearDays = 380;

	/// <summary>Four quarters ending within this many days count as the trailing twelve months.</summary>
	public const int MaxTrailingDays = 380;

	/// <summary>Statements found are looked up again after this long (a new 10-Q comes every ~3 months).</summary>
	public static readonly TimeSpan FoundTtl = TimeSpan.FromHours(12);

	/// <summary>A ticker without statements (ETFs, foreign filers) is asked again after this long.</summary>
	public static readonly TimeSpan NotFiledTtl = TimeSpan.FromDays(1);

	/// <summary>Lines whose values decide which periods exist (any of them reported).</summary>
	public static readonly IReadOnlyList<FinancialItem> PeriodItems =
	[
		FinancialItem.Revenue,
		FinancialItem.NetIncome,
		FinancialItem.OperatingCashFlow,
	];

	/// <summary>Every line, in the order shown.</summary>
	public static readonly IReadOnlyList<FinancialItemDefinitionDto> Items =
	[
		Flow(
			FinancialItem.Revenue,
			FinancialStatement.IncomeStatement,
			"Revenue",
			"Revenues",
			"RevenueFromContractWithCustomerExcludingAssessedTax",
			"RevenueFromContractWithCustomerIncludingAssessedTax",
			"SalesRevenueNet",
			"SalesRevenueGoodsNet",
			"SalesRevenueServicesNet",
			"RevenuesNetOfInterestExpense"
		),
		Flow(
			FinancialItem.CostOfRevenue,
			FinancialStatement.IncomeStatement,
			"Cost of revenue",
			"CostOfRevenue",
			"CostOfGoodsAndServicesSold",
			"CostOfGoodsSold",
			"CostOfServices"
		),
		Flow(
			FinancialItem.GrossProfit,
			FinancialStatement.IncomeStatement,
			"Gross profit",
			"GrossProfit"
		) with
		{
			ToolTip = "Revenue less cost of revenue where the company doesn't report it",
		},
		Flow(
			FinancialItem.ResearchAndDevelopment,
			FinancialStatement.IncomeStatement,
			"Research & development",
			"ResearchAndDevelopmentExpense",
			"ResearchAndDevelopmentExpenseExcludingAcquiredInProcessCost"
		),
		Flow(
			FinancialItem.SellingGeneralAdministrative,
			FinancialStatement.IncomeStatement,
			"SG&A",
			"SellingGeneralAndAdministrativeExpense",
			"GeneralAndAdministrativeExpense"
		) with
		{
			ToolTip = "Selling, general and administrative expenses",
		},
		Flow(
			FinancialItem.OperatingIncome,
			FinancialStatement.IncomeStatement,
			"Operating income",
			"OperatingIncomeLoss"
		),
		Flow(
			FinancialItem.PretaxIncome,
			FinancialStatement.IncomeStatement,
			"Pretax income",
			"IncomeLossFromContinuingOperationsBeforeIncomeTaxesExtraordinaryItemsNoncontrollingInterest",
			"IncomeLossFromContinuingOperationsBeforeIncomeTaxesMinorityInterestAndIncomeLossFromEquityMethodInvestments",
			"IncomeLossFromContinuingOperationsBeforeIncomeTaxesDomestic"
		),
		Flow(
			FinancialItem.IncomeTax,
			FinancialStatement.IncomeStatement,
			"Income tax",
			"IncomeTaxExpenseBenefit"
		),
		Flow(
			FinancialItem.NetIncome,
			FinancialStatement.IncomeStatement,
			"Net income",
			"NetIncomeLoss",
			"NetIncomeLossAvailableToCommonStockholdersBasic",
			"ProfitLoss"
		),
		PerShare(
			FinancialItem.EpsBasic,
			"EPS (basic)",
			"EarningsPerShareBasic",
			"EarningsPerShareBasicAndDiluted"
		),
		PerShare(
			FinancialItem.EpsDiluted,
			"EPS (diluted)",
			"EarningsPerShareDiluted",
			"EarningsPerShareBasicAndDiluted"
		),
		PerShare(
			FinancialItem.DilutedShares,
			"Diluted shares",
			"WeightedAverageNumberOfDilutedSharesOutstanding"
		) with
		{
			ToolTip = "Weighted average over the period",
		},
		Instant(
			FinancialItem.Cash,
			"Cash & equivalents",
			"CashAndCashEquivalentsAtCarryingValue",
			"CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalents",
			"Cash"
		),
		Instant(
			FinancialItem.ShortTermInvestments,
			"Short-term investments",
			"MarketableSecuritiesCurrent",
			"ShortTermInvestments",
			"AvailableForSaleSecuritiesDebtSecuritiesCurrent"
		),
		Instant(FinancialItem.CurrentAssets, "Current assets", "AssetsCurrent"),
		Instant(FinancialItem.TotalAssets, "Total assets", "Assets"),
		Instant(FinancialItem.CurrentLiabilities, "Current liabilities", "LiabilitiesCurrent"),
		Instant(
			FinancialItem.CurrentDebt,
			"Current debt",
			"DebtCurrent",
			"LongTermDebtCurrent",
			"LongTermDebtAndCapitalLeaseObligationsCurrent"
		) with
		{
			ToolTip = "Debt due within a year (commercial paper not included)",
		},
		Instant(
			FinancialItem.LongTermDebt,
			"Long-term debt",
			"LongTermDebtNoncurrent",
			"LongTermDebtAndCapitalLeaseObligations",
			"LongTermDebt"
		),
		Instant(FinancialItem.TotalLiabilities, "Total liabilities", "Liabilities") with
		{
			ToolTip =
				"Total liabilities and equity less equity where the company doesn't report it",
		},
		Instant(
			FinancialItem.Equity,
			"Shareholders' equity",
			"StockholdersEquity",
			"StockholdersEquityIncludingPortionAttributableToNoncontrollingInterest"
		),
		Instant(FinancialItem.LiabilitiesAndEquity, null, "LiabilitiesAndStockholdersEquity"),
		Flow(
			FinancialItem.OperatingCashFlow,
			FinancialStatement.CashFlow,
			"Operating cash flow",
			"NetCashProvidedByUsedInOperatingActivities",
			"NetCashProvidedByUsedInOperatingActivitiesContinuingOperations"
		),
		Flow(
			FinancialItem.CapitalExpenditure,
			FinancialStatement.CashFlow,
			"Capital expenditure",
			"PaymentsToAcquirePropertyPlantAndEquipment",
			"PaymentsToAcquireProductiveAssets"
		) with
		{
			ToolTip = "Paid for property, plant and equipment (as reported, a positive amount)",
		},
		Flow(FinancialItem.FreeCashFlow, FinancialStatement.CashFlow, "Free cash flow") with
		{
			ToolTip = "Operating cash flow less capital expenditure",
		},
		Flow(
			FinancialItem.InvestingCashFlow,
			FinancialStatement.CashFlow,
			"Investing cash flow",
			"NetCashProvidedByUsedInInvestingActivities",
			"NetCashProvidedByUsedInInvestingActivitiesContinuingOperations"
		),
		Flow(
			FinancialItem.FinancingCashFlow,
			FinancialStatement.CashFlow,
			"Financing cash flow",
			"NetCashProvidedByUsedInFinancingActivities",
			"NetCashProvidedByUsedInFinancingActivitiesContinuingOperations"
		),
		Flow(
			FinancialItem.DepreciationAmortization,
			FinancialStatement.CashFlow,
			"Depreciation & amortization",
			"DepreciationDepletionAndAmortization",
			"DepreciationAndAmortization",
			"DepreciationAmortizationAndAccretionNet",
			"Depreciation"
		),
		Flow(
			FinancialItem.ShareBasedCompensation,
			FinancialStatement.CashFlow,
			"Stock-based compensation",
			"ShareBasedCompensation",
			"AllocatedShareBasedCompensationExpense"
		),
		Flow(
			FinancialItem.DividendsPaid,
			FinancialStatement.CashFlow,
			"Dividends paid",
			"PaymentsOfDividends",
			"PaymentsOfDividendsCommonStock"
		),
		Flow(
			FinancialItem.ShareRepurchases,
			FinancialStatement.CashFlow,
			"Share buybacks",
			"PaymentsForRepurchaseOfCommonStock"
		),
	];

	/// <summary>Every concept any line reads (what is taken from the companyfacts answer).</summary>
	public static readonly IReadOnlyCollection<string> Concepts = Items
		.SelectMany(x => x.Concepts)
		.ToHashSet(StringComparer.Ordinal);

	private static FinancialItemDefinitionDto Flow(
		FinancialItem item,
		FinancialStatement statement,
		string label,
		params string[] concepts
	) => new(item, statement, FinancialValueKind.Flow, label, concepts);

	private static FinancialItemDefinitionDto PerShare(
		FinancialItem item,
		string label,
		params string[] concepts
	) =>
		new(item, FinancialStatement.IncomeStatement, FinancialValueKind.PerShare, label, concepts);

	private static FinancialItemDefinitionDto Instant(
		FinancialItem item,
		string? label,
		params string[] concepts
	) => new(item, FinancialStatement.BalanceSheet, FinancialValueKind.Instant, label, concepts);
}
