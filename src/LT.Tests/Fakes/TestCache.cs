using LT.Domain.Managers;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Fakes;

/// <summary>A real CacheManager over in-memory storage and settings, on a fake clock.</summary>
public class TestCache
{
	public TestCache() => LSCoreContainer.EnsureInitialized();

	public InMemoryCacheRepository Repository { get; } = new();
	public InMemorySettingsRepository Settings { get; } = new();
	public FakeTimeProvider Time { get; } =
		new(new DateTimeOffset(2026, 10, 23, 15, 0, 0, TimeSpan.Zero));

	public CacheManager Manager => new(Repository, Settings, Time);
}
