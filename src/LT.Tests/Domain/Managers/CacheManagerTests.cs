using LT.Contracts.Constants;
using LT.Contracts.Enums;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.Managers;

public class CacheManagerTests
{
	private readonly InMemoryCacheRepository _cache = new();
	private readonly InMemorySettingsRepository _settings = new();
	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 23, 15, 0, 0, TimeSpan.Zero)
	);
	private readonly CacheManager _manager;
	private int _fetches;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public CacheManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new CacheManager(_cache, _settings, _time);
	}

	private Task<List<int>> Fetch(CancellationToken _)
	{
		_fetches++;
		return Task.FromResult(new List<int> { _fetches });
	}

	private static Task<List<int>> Fail(CancellationToken _) =>
		throw new InvalidOperationException("Not connected");

	[Fact]
	public async Task Fresh_entry_is_served_without_fetching()
	{
		await _manager.GetOrFetchAsync(
			CacheType.Contracts,
			"k",
			TimeSpan.FromMinutes(1),
			Fetch,
			Ct
		);
		_time.Advance(TimeSpan.FromSeconds(30));

		var result = await _manager.GetOrFetchAsync(
			CacheType.Contracts,
			"k",
			TimeSpan.FromMinutes(1),
			Fetch,
			Ct
		);

		Assert.Equal(1, _fetches);
		Assert.Equal([1], result.Value);
		Assert.False(result.IsStale);
	}

	[Fact]
	public async Task Expired_entry_is_fetched_again()
	{
		await _manager.GetOrFetchAsync(
			CacheType.Contracts,
			"k",
			TimeSpan.FromMinutes(1),
			Fetch,
			Ct
		);
		_time.Advance(TimeSpan.FromMinutes(1));

		var result = await _manager.GetOrFetchAsync(
			CacheType.Contracts,
			"k",
			TimeSpan.FromMinutes(1),
			Fetch,
			Ct
		);

		Assert.Equal(2, _fetches);
		Assert.Equal([2], result.Value);
	}

	[Fact]
	public async Task Failure_serves_the_expired_copy_as_stale()
	{
		await _manager.GetOrFetchAsync(
			CacheType.Contracts,
			"k",
			TimeSpan.FromMinutes(1),
			Fetch,
			Ct
		);
		var fetchedAt = _time.GetUtcNow();
		_time.Advance(TimeSpan.FromHours(1));

		var result = await _manager.GetOrFetchAsync(
			CacheType.Contracts,
			"k",
			TimeSpan.FromMinutes(1),
			Fail,
			Ct
		);

		Assert.True(result.IsStale);
		Assert.Equal("Not connected", result.Error);
		Assert.Equal(fetchedAt, result.FetchedAt);
		Assert.Equal([1], result.Value);
	}

	[Fact]
	public async Task Failure_without_a_copy_propagates() =>
		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			_manager.GetOrFetchAsync(CacheType.Contracts, "k", TimeSpan.FromMinutes(1), Fail, Ct)
		);

	[Fact]
	public async Task Canceling_never_serves_a_stale_copy()
	{
		await _manager.GetOrFetchAsync(
			CacheType.Contracts,
			"k",
			TimeSpan.FromMinutes(1),
			Fetch,
			Ct
		);
		_time.Advance(TimeSpan.FromHours(1));

		await Assert.ThrowsAsync<OperationCanceledException>(() =>
			_manager.GetOrFetchAsync<List<int>>(
				CacheType.Contracts,
				"k",
				TimeSpan.FromMinutes(1),
				_ => throw new OperationCanceledException(),
				Ct
			)
		);
	}

	[Fact]
	public async Task Values_refused_by_canCache_are_not_stored()
	{
		await _manager.GetOrFetchAsync(
			CacheType.Contracts,
			"k",
			TimeSpan.FromMinutes(1),
			Fetch,
			Ct,
			_ => false
		);

		Assert.Empty(_cache.Entries);
	}

	[Fact]
	public async Task Disabled_type_is_neither_read_nor_written()
	{
		_manager.Save(CacheType.Executions, "k", new List<int> { 7 });
		_settings.Settings.General.CacheExecutions = false;

		var result = await _manager.GetOrFetchAsync(
			CacheType.Executions,
			"k",
			TimeSpan.FromMinutes(1),
			Fetch,
			Ct
		);
		_manager.Save(CacheType.Executions, "other", new List<int> { 8 });

		Assert.Equal([1], result.Value);
		Assert.Null(_manager.Get<List<int>>(CacheType.Executions, "k"));
		Assert.DoesNotContain(_cache.Entries.Keys, x => x.Key == "other");
		// other types keep working
		_manager.Save(CacheType.Contracts, "k", new List<int> { 9 });
		Assert.NotNull(_manager.Get<List<int>>(CacheType.Contracts, "k"));
	}

	[Fact]
	public void Master_switch_disables_every_type()
	{
		_settings.Settings.General.CacheEnabled = false;

		foreach (var type in Enum.GetValues<CacheType>())
		{
			_manager.Save(type, "k", new List<int> { 1 });
			Assert.False(_manager.IsEnabled(type));
		}
		Assert.Empty(_cache.Entries);
	}

	[Fact]
	public void Saving_past_the_limit_evicts_the_least_recently_written()
	{
		_settings.Settings.General.CacheMaxSizeMb = CacheConstants.MaxSizeMbMin;
		var big = new string('x', 4 * 1024 * 1024);

		_manager.Save(CacheType.ChartBars, "oldest", big);
		_time.Advance(TimeSpan.FromMinutes(1));
		_manager.Save(CacheType.ChartBars, "middle", big);
		_time.Advance(TimeSpan.FromMinutes(1));
		_manager.Save(CacheType.ChartBars, "newest", big);

		Assert.Null(_manager.Get<string>(CacheType.ChartBars, "oldest"));
		Assert.NotNull(_manager.Get<string>(CacheType.ChartBars, "middle"));
		Assert.NotNull(_manager.Get<string>(CacheType.ChartBars, "newest"));
		Assert.True(
			_manager.GetSizeBytes() <= CacheConstants.MaxSizeMbMin * CacheConstants.BytesPerMb
		);
	}

	[Fact]
	public void Startup_drops_legacy_data_and_applies_the_limit()
	{
		_manager.Save(CacheType.ChartBars, "big", new string('x', 4 * 1024 * 1024));
		_settings.Settings.General.CacheMaxSizeMb = 1;

		_manager.RunStartupMaintenance();

		Assert.True(_cache.LegacyRemoved);
		Assert.Empty(_cache.Entries);
	}

	[Fact]
	public void Clear_removes_everything()
	{
		_manager.Save(CacheType.ChartBars, "a", "1");
		_manager.Save(CacheType.Snapshots, "b", "2");

		_manager.Clear();

		Assert.Equal(0, _manager.GetSizeBytes());
		Assert.Null(_manager.Get<string>(CacheType.ChartBars, "a"));
	}
}
