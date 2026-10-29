namespace LT.Contracts.Dtos.Fundamentals;

/// <summary>The part of a company's SEC companyfacts the financials read.</summary>
public class SecCompanyFactsDto
{
	public string? EntityName { get; set; }

	/// <summary>Every value of the asked us-gaap concepts, whatever their unit (USD, USD/shares, shares).</summary>
	public IReadOnlyList<SecFactDto> Facts { get; set; } = [];
}
