using LT.Domain.Managers;
using LT.Repository.Repositories;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Fakes;

/// <summary>The real news archive (repository and manager) in a temp folder deleted on dispose.</summary>
public sealed class TestNewsArchive : IDisposable
{
	public TestNewsArchive(FakeTimeProvider? time = null)
	{
		LSCoreContainer.EnsureInitialized();
		Time =
			time
			?? new FakeTimeProvider(new DateTimeOffset(2026, 10, 29, 12, 30, 0, TimeSpan.Zero));
		Directory = Path.Combine(Path.GetTempPath(), $"lt-news-tests-{Guid.NewGuid():N}");
		Repository = new NewsArchiveRepository(Directory);
		SettingsManager = new SettingsManager(Settings, new FakeIbkrApiClient());
		Manager = new NewsArchiveManager(Repository, SettingsManager, Time);
	}

	public string Directory { get; }
	public FakeTimeProvider Time { get; }
	public InMemorySettingsRepository Settings { get; } = new();
	public SettingsManager SettingsManager { get; }
	public NewsArchiveRepository Repository { get; }
	public NewsArchiveManager Manager { get; }

	public void Dispose()
	{
		if (System.IO.Directory.Exists(Directory))
			System.IO.Directory.Delete(Directory, recursive: true);
	}
}
