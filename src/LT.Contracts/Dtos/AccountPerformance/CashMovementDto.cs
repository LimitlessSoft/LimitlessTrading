using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.AccountPerformance;

/// <summary>A deposit, withdrawal or transfer of an account (from Flex), for display.</summary>
public class CashMovementDto
{
	public DateOnly Date { get; set; }
	public CashMovementKind Kind { get; set; }

	/// <summary>In the account's base currency, positive in, negative out.</summary>
	public double Amount { get; set; }
	public string Currency { get; set; } = string.Empty;
	public double AmountInCurrency { get; set; }
	public string Description { get; set; } = string.Empty;
}
