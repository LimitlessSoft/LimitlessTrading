using LT.Contracts.Entities;
using LT.Repository.Repositories;

namespace LT.Tests.Repository;

/// <summary>The real patch-notes.json, in a temp folder deleted after each test.</summary>
public sealed class PatchNotesRepositoryTests : IDisposable
{
	private readonly string _directory = Path.Combine(
		Path.GetTempPath(),
		$"lt-patch-notes-tests-{Guid.NewGuid():N}"
	);
	private readonly PatchNotesRepository _repository;

	public PatchNotesRepositoryTests() =>
		_repository = new PatchNotesRepository(Path.Combine(_directory, "patch-notes.json"));

	public void Dispose()
	{
		if (Directory.Exists(_directory))
			Directory.Delete(_directory, recursive: true);
	}

	[Fact]
	public void Missing_file_is_an_empty_state() => Assert.Empty(_repository.Get().Releases);

	[Fact]
	public void Round_trips_the_state()
	{
		_repository.Save(
			new PatchNotesStateEntity
			{
				Releases = [new PatchNotesReleaseEntity { Tag = "v0.10.0", Body = "* A" }],
				LastSeenVersion = "0.10.0",
			}
		);

		var state = _repository.Get();

		Assert.Equal("* A", Assert.Single(state.Releases).Body);
		Assert.Equal("0.10.0", state.LastSeenVersion);
	}

	[Fact]
	public void Unreadable_file_is_an_empty_state()
	{
		Directory.CreateDirectory(_directory);
		File.WriteAllText(Path.Combine(_directory, "patch-notes.json"), "{ not json");

		Assert.Null(_repository.Get().LastSeenVersion);
	}
}
