using LT.Contracts.Enums;

namespace LT.Contracts.Entities;

/// <summary>
/// One deposit, withdrawal or transfer of an account, from Flex. Shown on the Account
/// performance charts and calendar; the P&amp;L math uses the day's <see cref="AccountDaySnapshotEntity.CashFlow"/>.
/// </summary>
public class CashMovementEntity
{
	/// <summary>Dedupe key: IB's transactionID, else built from the row (AccountPerformanceHelpers.GetCashMovementKey).</summary>
	public string Id { get; set; } = string.Empty;
	public DateOnly Date { get; set; }
	public CashMovementKind Kind { get; set; }

	/// <summary>In the account's base currency, positive in, negative out.</summary>
	public double Amount { get; set; }
	public string Currency { get; set; } = string.Empty;
	public double AmountInCurrency { get; set; }
	public string Description { get; set; } = string.Empty;
}
