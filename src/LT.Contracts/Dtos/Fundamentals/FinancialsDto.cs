using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Fundamentals;

/// <summary>A company's statements from its SEC filings, newest period first.</summary>
public class FinancialsDto
{
	public FinancialsStatus Status { get; set; }

	/// <summary>The company's name at the SEC ("Apple Inc."); null when it wasn't found.</summary>
	public string? EntityName { get; set; }

	/// <summary>The fiscal years, newest first (at most <see cref="Constants.FinancialsConstants.AnnualPeriods"/>).</summary>
	public IReadOnlyList<FinancialPeriodDto> Annual { get; set; } = [];

	/// <summary>The quarters, newest first (at most <see cref="Constants.FinancialsConstants.QuarterlyPeriods"/>).</summary>
	public IReadOnlyList<FinancialPeriodDto> Quarterly { get; set; } = [];

	/// <summary>
	/// The last four quarters added up, per flow line with all four (null without four
	/// consecutive quarters): what the valuation ratios use.
	/// </summary>
	public Dictionary<FinancialItem, decimal> TrailingTwelveMonths { get; set; } = [];
}
