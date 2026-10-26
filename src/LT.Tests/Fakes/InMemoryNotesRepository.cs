using System.Text.Json;
using LT.Contracts.Entities;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Tests.Fakes;

public class InMemoryNotesRepository : INotesRepository
{
	private string? _json;

	public int SaveCount { get; private set; }

	/// <summary>Makes the next saves fail like a locked or read-only file.</summary>
	public bool FailSaves { get; set; }

	public NotesEntity? Saved =>
		_json == null ? null : JsonSerializer.Deserialize<NotesEntity>(_json);

	public NotesEntity Get() => Saved ?? new NotesEntity();

	// Like the JSON file: what was saved doesn't change with the caller's object
	public void Save(NotesEntity notes)
	{
		if (FailSaves)
			throw new IOException("The file is locked");
		_json = JsonSerializer.Serialize(notes);
		SaveCount++;
	}
}
