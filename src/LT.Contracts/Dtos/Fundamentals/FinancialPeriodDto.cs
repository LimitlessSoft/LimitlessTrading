using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Fundamentals;

/// <summary>One fiscal year or quarter of a company's statements.</summary>
public class FinancialPeriodDto
{
	/// <summary>The period's first day (from the filings, or the day after the previous period).</summary>
	public DateOnly Start { get; set; }
	public DateOnly End { get; set; }

	/// <summary>The fiscal year the period belongs to, named by the year it ends in (Apple's FY2025 ends Sep 2025).</summary>
	public int FiscalYear { get; set; }

	/// <summary>1–4 for a quarter; null for a fiscal year.</summary>
	public int? FiscalQuarter { get; set; }

	public Dictionary<FinancialItem, decimal> Values { get; set; } = [];

	/// <summary>
	/// Quarter lines not reported for the quarter as such but worked out from two years to date
	/// (Q2: six months less three; Q4: the 10-K's year less nine months), and lines computed from
	/// such a line (free cash flow of a worked-out quarter).
	/// </summary>
	public HashSet<FinancialItem> Derived { get; set; } = [];

	public decimal? Get(FinancialItem item) =>
		Values.TryGetValue(item, out var value) ? value : null;
}
