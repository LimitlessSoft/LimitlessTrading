using LT.Contracts.Entities;
using LT.Repository.Repositories;

namespace LT.Tests.Repository;

/// <summary>The real notes.json, in a temp folder deleted after each test.</summary>
public sealed class NotesRepositoryTests : IDisposable
{
	private readonly string _directory = Path.Combine(
		Path.GetTempPath(),
		$"lt-notes-tests-{Guid.NewGuid():N}"
	);
	private readonly string _path;
	private readonly NotesRepository _repository;

	public NotesRepositoryTests()
	{
		_path = Path.Combine(_directory, "notes.json");
		_repository = new NotesRepository(_path);
	}

	public void Dispose()
	{
		if (Directory.Exists(_directory))
			Directory.Delete(_directory, recursive: true);
	}

	[Fact]
	public void Missing_file_has_no_notes() => Assert.Empty(_repository.Get().Notes);

	[Fact]
	public void Round_trips_the_notes()
	{
		var at = DateTimeOffset.Parse("2026-10-26T12:00:00Z");
		_repository.Save(
			new NotesEntity
			{
				Notes =
				{
					["n1"] = new NoteEntity { Text = "line 1\nline 2", UpdatedAt = at },
				},
			}
		);

		var note = Assert.Single(new NotesRepository(_path).Get().Notes);
		Assert.Equal("n1", note.Key);
		Assert.Equal(("line 1\nline 2", at), (note.Value.Text, note.Value.UpdatedAt));
	}

	[Fact]
	public void An_unreadable_file_is_kept_aside_and_reads_as_no_notes()
	{
		Directory.CreateDirectory(_directory);
		File.WriteAllText(_path, "{ not json");

		Assert.Empty(_repository.Get().Notes);
		Assert.Equal("{ not json", File.ReadAllText(_path + ".corrupt"));
	}
}
