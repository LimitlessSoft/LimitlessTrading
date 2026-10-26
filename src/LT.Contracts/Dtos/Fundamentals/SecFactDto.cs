namespace LT.Contracts.Dtos.Fundamentals;

/// <summary>One reported value of an SEC XBRL concept (one entry of its "units" list).</summary>
public class SecFactDto
{
	/// <summary>The date the value is as of.</summary>
	public DateOnly End { get; set; }
	public DateOnly Filed { get; set; }
	public decimal Value { get; set; }

	/// <summary>The report it came from: 10-K, 10-Q, 20-F …</summary>
	public string Form { get; set; } = string.Empty;
}
