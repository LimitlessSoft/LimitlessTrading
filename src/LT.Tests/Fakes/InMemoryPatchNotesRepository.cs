using System.Text.Json;
using LT.Contracts.Entities;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Tests.Fakes;

public class InMemoryPatchNotesRepository : IPatchNotesRepository
{
	private string? _json;

	public int Saves { get; private set; }

	public PatchNotesStateEntity? Saved =>
		_json == null ? null : JsonSerializer.Deserialize<PatchNotesStateEntity>(_json);

	public PatchNotesStateEntity Get() => Saved ?? new PatchNotesStateEntity();

	// Like the JSON file: what was saved doesn't change with the caller's object
	public void Save(PatchNotesStateEntity state)
	{
		Saves++;
		_json = JsonSerializer.Serialize(state);
	}
}
