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
			"Money from selling the company's goods and services, before any costs.",
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
			"The direct cost of the goods and services sold: materials, production, delivery.",
			"CostOfRevenue",
			"CostOfGoodsAndServicesSold",
			"CostOfGoodsSold",
			"CostOfServices"
		),
		Flow(
			FinancialItem.GrossProfit,
			FinancialStatement.IncomeStatement,
			"Gross profit",
			"Revenue less the cost of revenue: what is left to pay for running the business.",
			"GrossProfit"
		) with
		{
			ToolTip = "Revenue less cost of revenue where the company doesn't report it",
		},
		Flow(
			FinancialItem.ResearchAndDevelopment,
			FinancialStatement.IncomeStatement,
			"Research & development",
			"Spending on research and on developing new products.",
			"ResearchAndDevelopmentExpense",
			"ResearchAndDevelopmentExpenseExcludingAcquiredInProcessCost"
		),
		Flow(
			FinancialItem.SellingGeneralAdministrative,
			FinancialStatement.IncomeStatement,
			"SG&A",
			"Selling, general and administrative expenses: sales, marketing, management and other overhead.",
			"SellingGeneralAndAdministrativeExpense",
			"GeneralAndAdministrativeExpense"
		),
		Flow(
			FinancialItem.OperatingIncome,
			FinancialStatement.IncomeStatement,
			"Operating income",
			"Profit from the core business: gross profit less operating expenses, before interest and taxes.",
			"OperatingIncomeLoss"
		),
		Flow(
			FinancialItem.PretaxIncome,
			FinancialStatement.IncomeStatement,
			"Pretax income",
			"Profit before income tax, after interest and other non-operating items.",
			"IncomeLossFromContinuingOperationsBeforeIncomeTaxesExtraordinaryItemsNoncontrollingInterest",
			"IncomeLossFromContinuingOperationsBeforeIncomeTaxesMinorityInterestAndIncomeLossFromEquityMethodInvestments",
			"IncomeLossFromContinuingOperationsBeforeIncomeTaxesDomestic"
		),
		Flow(
			FinancialItem.IncomeTax,
			FinancialStatement.IncomeStatement,
			"Income tax",
			"Income tax expense for the period (negative: a tax benefit).",
			"IncomeTaxExpenseBenefit"
		),
		Flow(
			FinancialItem.NetIncome,
			FinancialStatement.IncomeStatement,
			"Net income",
			"The bottom line: profit after every expense, interest and tax.",
			"NetIncomeLoss",
			"NetIncomeLossAvailableToCommonStockholdersBasic",
			"ProfitLoss"
		),
		PerShare(
			FinancialItem.EpsBasic,
			"EPS (basic)",
			"Earnings per share: net income divided by the average number of shares outstanding.",
			"EarningsPerShareBasic",
			"EarningsPerShareBasicAndDiluted"
		),
		PerShare(
			FinancialItem.EpsDiluted,
			"EPS (diluted)",
			"Earnings per share counting the shares options, convertibles and other rights would add; the figure usually quoted.",
			"EarningsPerShareDiluted",
			"EarningsPerShareBasicAndDiluted"
		),
		PerShare(
			FinancialItem.DilutedShares,
			"Diluted shares",
			"The share count diluted EPS is divided by: shares outstanding plus those options and convertibles would add.",
			"WeightedAverageNumberOfDilutedSharesOutstanding"
		) with
		{
			ToolTip = "Weighted average over the period",
		},
		Instant(
			FinancialItem.Cash,
			"Cash & equivalents",
			"Cash and investments so short-term and liquid they count as cash.",
			"CashAndCashEquivalentsAtCarryingValue",
			"CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalents",
			"Cash"
		),
		Instant(
			FinancialItem.ShortTermInvestments,
			"Short-term investments",
			"Marketable securities the company can turn into cash within a year.",
			"MarketableSecuritiesCurrent",
			"ShortTermInvestments",
			"AvailableForSaleSecuritiesDebtSecuritiesCurrent"
		),
		Instant(
			FinancialItem.CurrentAssets,
			"Current assets",
			"Assets expected to turn into cash within a year: cash, investments, receivables, inventory.",
			"AssetsCurrent"
		),
		Instant(
			FinancialItem.TotalAssets,
			"Total assets",
			"Everything the company owns or is owed.",
			"Assets"
		),
		Instant(
			FinancialItem.CurrentLiabilities,
			"Current liabilities",
			"What the company owes that is due within a year.",
			"LiabilitiesCurrent"
		),
		Instant(
			FinancialItem.CurrentDebt,
			"Current debt",
			"Borrowings due within a year.",
			"DebtCurrent",
			"LongTermDebtCurrent",
			"LongTermDebtAndCapitalLeaseObligationsCurrent"
		) with
		{
			ToolTip = "Commercial paper not included",
		},
		Instant(
			FinancialItem.LongTermDebt,
			"Long-term debt",
			"Borrowings due in more than a year.",
			"LongTermDebtNoncurrent",
			"LongTermDebtAndCapitalLeaseObligations",
			"LongTermDebt"
		),
		Instant(
			FinancialItem.TotalLiabilities,
			"Total liabilities",
			"Everything the company owes.",
			"Liabilities"
		) with
		{
			ToolTip =
				"Total liabilities and equity less equity where the company doesn't report it",
		},
		Instant(
			FinancialItem.Equity,
			"Shareholders' equity",
			"Book value: total assets less total liabilities, what belongs to the shareholders.",
			"StockholdersEquity",
			"StockholdersEquityIncludingPortionAttributableToNoncontrollingInterest"
		),
		Instant(FinancialItem.LiabilitiesAndEquity, null, null, "LiabilitiesAndStockholdersEquity"),
		Flow(
			FinancialItem.OperatingCashFlow,
			FinancialStatement.CashFlow,
			"Operating cash flow",
			"Cash the business itself brought in during the period.",
			"NetCashProvidedByUsedInOperatingActivities",
			"NetCashProvidedByUsedInOperatingActivitiesContinuingOperations"
		),
		Flow(
			FinancialItem.CapitalExpenditure,
			FinancialStatement.CashFlow,
			"Capital expenditure",
			"Cash spent on property, plant and equipment to keep and grow the business.",
			"PaymentsToAcquirePropertyPlantAndEquipment",
			"PaymentsToAcquireProductiveAssets"
		) with
		{
			ToolTip = "As reported, a positive amount",
		},
		Flow(
			FinancialItem.FreeCashFlow,
			FinancialStatement.CashFlow,
			"Free cash flow",
			"Cash left after investing in the business, free for debt, dividends and buybacks."
		) with
		{
			ToolTip = "Operating cash flow less capital expenditure",
		},
		Flow(
			FinancialItem.InvestingCashFlow,
			FinancialStatement.CashFlow,
			"Investing cash flow",
			"Cash spent on (negative) or received from (positive) investments, equipment and acquisitions.",
			"NetCashProvidedByUsedInInvestingActivities",
			"NetCashProvidedByUsedInInvestingActivitiesContinuingOperations"
		),
		Flow(
			FinancialItem.FinancingCashFlow,
			FinancialStatement.CashFlow,
			"Financing cash flow",
			"Cash raised from (positive) or returned to (negative) lenders and shareholders: borrowing, repayments, dividends, buybacks.",
			"NetCashProvidedByUsedInFinancingActivities",
			"NetCashProvidedByUsedInFinancingActivitiesContinuingOperations"
		),
		Flow(
			FinancialItem.DepreciationAmortization,
			FinancialStatement.CashFlow,
			"Depreciation & amortization",
			"The cost of long-lived assets spread over their useful life: an expense paid in an earlier period, added back to cash flow.",
			"DepreciationDepletionAndAmortization",
			"DepreciationAndAmortization",
			"DepreciationAmortizationAndAccretionNet",
			"Depreciation"
		),
		Flow(
			FinancialItem.ShareBasedCompensation,
			FinancialStatement.CashFlow,
			"Stock-based compensation",
			"Pay in shares or options: an expense paid without cash, added back to cash flow, but it dilutes shareholders.",
			"ShareBasedCompensation",
			"AllocatedShareBasedCompensationExpense"
		),
		Flow(
			FinancialItem.DividendsPaid,
			FinancialStatement.CashFlow,
			"Dividends paid",
			"Cash paid to shareholders as dividends.",
			"PaymentsOfDividends",
			"PaymentsOfDividendsCommonStock"
		),
		Flow(
			FinancialItem.ShareRepurchases,
			FinancialStatement.CashFlow,
			"Share buybacks",
			"Cash spent buying back the company's own shares.",
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
		string description,
		params string[] concepts
	) => new(item, statement, FinancialValueKind.Flow, label, concepts, Description: description);

	private static FinancialItemDefinitionDto PerShare(
		FinancialItem item,
		string label,
		string description,
		params string[] concepts
	) =>
		new(
			item,
			FinancialStatement.IncomeStatement,
			FinancialValueKind.PerShare,
			label,
			concepts,
			Description: description
		);

	private static FinancialItemDefinitionDto Instant(
		FinancialItem item,
		string? label,
		string? description,
		params string[] concepts
	) =>
		new(
			item,
			FinancialStatement.BalanceSheet,
			FinancialValueKind.Instant,
			label,
			concepts,
			Description: description
		);
}
