using System.Text.Json;
using LT.Contracts.Entities;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Repository.Repositories;

/// <summary>
/// Recorded account performance, one JSON file per account in the app-data folder
/// (account-history/&lt;account&gt;.json). Registered as a singleton.
/// </summary>
public class AccountHistoryRepository : IAccountHistoryRepository
{
	private readonly Lock _lock = new();
	private readonly string _directory = AppPathHelpers.GetAccountHistoryDirectory();

	public AccountHistoryEntity Get(string account)
	{
		var path = GetPath(account);
		lock (_lock)
		{
			if (!File.Exists(path))
				return new AccountHistoryEntity { Account = account };

			try
			{
				return JsonSerializer.Deserialize<AccountHistoryEntity>(File.ReadAllText(path))
					?? new AccountHistoryEntity { Account = account };
			}
			catch (JsonException)
			{
				// Kept aside: the next save would otherwise overwrite what was recorded
				File.Copy(path, path + ".bad", overwrite: true);
				return new AccountHistoryEntity { Account = account };
			}
		}
	}

	public void Save(AccountHistoryEntity history)
	{
		var path = GetPath(history.Account);
		lock (_lock)
		{
			Directory.CreateDirectory(_directory);
			// Written aside and moved over, so a crash mid-write can't lose the recorded history
			var temp = path + ".tmp";
			File.WriteAllText(temp, JsonSerializer.Serialize(history));
			File.Move(temp, path, overwrite: true);
		}
	}

	private string GetPath(string account)
	{
		var name = string.Concat(
			account.Select(x => Path.GetInvalidFileNameChars().Contains(x) ? '_' : x)
		);
		return Path.Combine(_directory, $"{name}.json");
	}
}
