using LT.Contracts.Entities;

namespace LT.Contracts.Interfaces.IRepositories;

public interface IAccountHistoryRepository
{
	/// <summary>The recorded days of <paramref name="account"/>; empty when none.</summary>
	AccountHistoryEntity Get(string account);
	void Save(AccountHistoryEntity history);
}
