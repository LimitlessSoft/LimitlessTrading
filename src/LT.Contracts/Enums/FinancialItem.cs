namespace LT.Contracts.Enums;

/// <summary>
/// One line of a company's financial statements, read from its SEC filings
/// (<see cref="Constants.FinancialsConstants.Items"/>); add new ones at the end.
/// </summary>
public enum FinancialItem
{
	Revenue = 0,
	CostOfRevenue = 1,
	GrossProfit = 2,
	ResearchAndDevelopment = 3,
	SellingGeneralAdministrative = 4,
	OperatingIncome = 5,
	PretaxIncome = 6,
	IncomeTax = 7,
	NetIncome = 8,
	EpsBasic = 9,
	EpsDiluted = 10,
	DilutedShares = 11,
	Cash = 12,
	ShortTermInvestments = 13,
	CurrentAssets = 14,
	TotalAssets = 15,
	CurrentLiabilities = 16,
	CurrentDebt = 17,
	LongTermDebt = 18,
	TotalLiabilities = 19,
	Equity = 20,
	LiabilitiesAndEquity = 21,
	OperatingCashFlow = 22,
	CapitalExpenditure = 23,
	FreeCashFlow = 24,
	InvestingCashFlow = 25,
	FinancingCashFlow = 26,
	DepreciationAmortization = 27,
	ShareBasedCompensation = 28,
	DividendsPaid = 29,
	ShareRepurchases = 30,
}
