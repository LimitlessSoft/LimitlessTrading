using System.Text.Json;
using LT.Contracts.Entities;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Tests.Fakes;

public class InMemorySharesOutstandingRepository : ISharesOutstandingRepository
{
	private readonly Dictionary<string, string> _json = new(StringComparer.OrdinalIgnoreCase);

	public SharesOutstandingEntity? Get(string ticker) =>
		_json.TryGetValue(ticker, out var json)
			? JsonSerializer.Deserialize<SharesOutstandingEntity>(json)
			: null;

	// Like the JSON file: what was saved doesn't change with the caller's object
	public void Save(string ticker, SharesOutstandingEntity entry) =>
		_json[ticker] = JsonSerializer.Serialize(entry);
}
