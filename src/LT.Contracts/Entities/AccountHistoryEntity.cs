namespace LT.Contracts.Entities;

/// <summary>The recorded days of one account, oldest first (one file per account).</summary>
public class AccountHistoryEntity
{
	public string Account { get; set; } = string.Empty;
	public List<AccountDaySnapshotEntity> Days { get; set; } = [];
}
