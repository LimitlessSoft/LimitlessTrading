namespace LT.Contracts.Entities;

/// <summary>The recorded days of one account, oldest first (one file per account).</summary>
public class AccountHistoryEntity
{
	public string Account { get; set; } = string.Empty;
	public List<AccountDaySnapshotEntity> Days { get; set; } = [];

	/// <summary>Deposits, withdrawals and transfers from Flex, oldest first (older files: none).</summary>
	public List<CashMovementEntity> CashMovements { get; set; } = [];
}
