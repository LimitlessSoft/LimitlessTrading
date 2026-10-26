using System.Text.Json;
using LT.Contracts.Entities;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Tests.Fakes;

public class InMemoryFlexImportStateRepository : IFlexImportStateRepository
{
	private string? _json;

	public FlexImportStateEntity? Saved =>
		_json == null ? null : JsonSerializer.Deserialize<FlexImportStateEntity>(_json);

	public FlexImportStateEntity Get() => Saved ?? new FlexImportStateEntity();

	// Like the JSON file: what was saved doesn't change with the caller's object
	public void Save(FlexImportStateEntity state) => _json = JsonSerializer.Serialize(state);
}
