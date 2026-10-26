using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Flex;

/// <summary>
/// A deposit / withdrawal (Cash Transactions) or a transfer (Transfers): positive in, negative
/// out, converted to the account's base currency.
/// </summary>
public class FlexCashFlowDto
{
	public string Account { get; set; } = string.Empty;
	public DateOnly Date { get; set; }

	/// <summary>In the account's base currency (amount × fxRateToBase).</summary>
	public double Amount { get; set; }
	public string Description { get; set; } = string.Empty;
	public CashMovementKind Kind { get; set; }

	/// <summary>IB's transactionID; empty when the query doesn't have it.</summary>
	public string TransactionId { get; set; } = string.Empty;

	/// <summary>The currency the movement was made in, and the amount in it.</summary>
	public string Currency { get; set; } = string.Empty;
	public double AmountInCurrency { get; set; }
}
