namespace LT.Contracts.Enums;

/// <summary>What kind of money movement an account history entry is. Persisted: add values at the end.</summary>
public enum CashMovementKind
{
	/// <summary>Money paid in from outside IB (Cash Transactions → Deposits &amp; Withdrawals).</summary>
	Deposit,

	/// <summary>Money paid out of IB (Cash Transactions → Deposits &amp; Withdrawals).</summary>
	Withdrawal,

	/// <summary>Money or positions moved in from another account (internal transfer, ACATS …).</summary>
	TransferIn,

	/// <summary>Money or positions moved out to another account (internal transfer, ACATS …).</summary>
	TransferOut,
}
