namespace LT.Contracts.Enums;

/// <summary>How a <see cref="FinancialItem"/> is reported; add new ones at the end.</summary>
public enum FinancialValueKind
{
	/// <summary>
	/// An amount over a period that adds up (revenue, cash flows): a quarter missing from the
	/// filings is the year to date less the previous year to date (Q4: the 10-K less Q1–Q3).
	/// </summary>
	Flow = 0,

	/// <summary>Per share or a share count over a period: not derived from other periods.</summary>
	PerShare = 1,

	/// <summary>A balance as of the period's end (balance sheet).</summary>
	Instant = 2,
}
