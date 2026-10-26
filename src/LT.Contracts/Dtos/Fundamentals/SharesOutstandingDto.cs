namespace LT.Contracts.Dtos.Fundamentals;

/// <summary>A company's shares outstanding as of a date (all classes, from its SEC filings).</summary>
public class SharesOutstandingDto
{
	public long Shares { get; set; }
	public DateOnly AsOf { get; set; }
}
