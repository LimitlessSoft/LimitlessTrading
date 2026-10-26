using LT.Contracts.Entities;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Tests.Fakes;

public class InMemoryAccountHistoryRepository : IAccountHistoryRepository
{
	public Dictionary<string, AccountHistoryEntity> Entries { get; } = [];
	public int SaveCount { get; private set; }

	public AccountHistoryEntity Get(string account) =>
		Entries.TryGetValue(account, out var history)
			? Clone(history)
			: new AccountHistoryEntity { Account = account };

	public void Save(AccountHistoryEntity history)
	{
		SaveCount++;
		Entries[history.Account] = Clone(history);
	}

	// Like the JSON file: what was saved doesn't change with the caller's objects
	private static AccountHistoryEntity Clone(AccountHistoryEntity history) =>
		System.Text.Json.JsonSerializer.Deserialize<AccountHistoryEntity>(
			System.Text.Json.JsonSerializer.Serialize(history)
		)!;
}
