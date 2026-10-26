namespace LT.Contracts.Dtos.Flex;

/// <summary>An account's net asset value at the end of an IB report date, in its base currency.</summary>
public class FlexNavDto
{
	public string Account { get; set; } = string.Empty;

	/// <summary>IB's report date (a US trading day).</summary>
	public DateOnly Date { get; set; }
	public decimal Total { get; set; }
	public string Currency { get; set; } = string.Empty;
}
