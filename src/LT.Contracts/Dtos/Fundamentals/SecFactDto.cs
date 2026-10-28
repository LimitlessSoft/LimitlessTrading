namespace LT.Contracts.Dtos.Fundamentals;

/// <summary>One reported value of an SEC XBRL concept (one entry of its "units" list).</summary>
public class SecFactDto
{
	/// <summary>The XBRL concept's name: EntityCommonStockSharesOutstanding …</summary>
	public string Concept { get; set; } = string.Empty;

	/// <summary>Where a period starts (weighted averages); null for a count as of <see cref="End"/>.</summary>
	public DateOnly? Start { get; set; }

	/// <summary>The date the value is as of.</summary>
	public DateOnly End { get; set; }
	public DateOnly Filed { get; set; }
	public decimal Value { get; set; }

	/// <summary>The report it came from: 10-K, 10-Q, 20-F …</summary>
	public string Form { get; set; } = string.Empty;

	/// <summary>
	/// The share class a filing's own count is for (its class-of-stock member: CommonClassAMember);
	/// null for a count of all classes. The SEC's JSON APIs only carry the latter.
	/// </summary>
	public string? ShareClass { get; set; }
}
