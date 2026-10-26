namespace LT.Contracts.Dtos.Flex;

/// <summary>A deposit (positive) or withdrawal (negative), converted to the account's base currency.</summary>
public class FlexCashFlowDto
{
	public string Account { get; set; } = string.Empty;
	public DateOnly Date { get; set; }
	public double Amount { get; set; }
	public string Description { get; set; } = string.Empty;
}
