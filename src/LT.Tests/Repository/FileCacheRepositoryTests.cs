using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Repository.Repositories;

namespace LT.Tests.Repository;

/// <summary>The real file cache, in a temp folder deleted after each test.</summary>
public sealed class FileCacheRepositoryTests : IDisposable
{
	private readonly string _directory = Path.Combine(
		Path.GetTempPath(),
		$"lt-cache-tests-{Guid.NewGuid():N}"
	);
	private readonly FileCacheRepository _repository;
	private static readonly DateTimeOffset T0 = new(2026, 10, 23, 15, 0, 0, TimeSpan.Zero);

	public FileCacheRepositoryTests() => _repository = new FileCacheRepository(_directory);

	public void Dispose()
	{
		if (Directory.Exists(_directory))
			Directory.Delete(_directory, recursive: true);
	}

	private void Save(CacheType type, string key, string value, DateTimeOffset at) =>
		_repository.Save(
			type,
			new CacheEntryEntity<string>
			{
				Key = key,
				FetchedAt = at,
				Value = value,
			}
		);

	[Fact]
	public void Round_trips_an_entry_under_its_type_folder()
	{
		Save(CacheType.Contracts, "AAPL|STK/SMART", "v", T0);

		var entry = _repository.Get<string>(CacheType.Contracts, "AAPL|STK/SMART");

		Assert.Equal("v", entry?.Value);
		Assert.Equal(T0, entry?.FetchedAt);
		Assert.Single(Directory.GetFiles(Path.Combine(_directory, nameof(CacheType.Contracts))));
		Assert.Null(_repository.Get<string>(CacheType.Executions, "AAPL|STK/SMART"));
	}

	[Fact]
	public void Missing_and_corrupt_entries_are_misses()
	{
		Save(CacheType.Contracts, "k", "v", T0);
		foreach (var file in Directory.GetFiles(_directory, "*.json", SearchOption.AllDirectories))
			File.WriteAllText(file, "{ not json");

		Assert.Null(_repository.Get<string>(CacheType.Contracts, "k"));
		Assert.Null(_repository.Get<string>(CacheType.Contracts, "missing"));
	}

	[Fact]
	public void Trim_evicts_the_least_recently_written_first()
	{
		var value = new string('x', 1000);
		Save(CacheType.ChartBars, "old", value, T0);
		Save(CacheType.Contracts, "mid", value, T0.AddMinutes(1));
		Save(CacheType.Snapshots, "new", value, T0.AddMinutes(2));
		var total = _repository.GetSizeBytes();

		_repository.Trim(total - 1);

		Assert.Null(_repository.Get<string>(CacheType.ChartBars, "old"));
		Assert.NotNull(_repository.Get<string>(CacheType.Contracts, "mid"));
		Assert.NotNull(_repository.Get<string>(CacheType.Snapshots, "new"));
		Assert.True(_repository.GetSizeBytes() < total);
	}

	[Fact]
	public void Clear_deletes_every_entry()
	{
		Save(CacheType.ChartBars, "a", "1", T0);
		Save(CacheType.Snapshots, "b", "2", T0);

		_repository.Clear();

		Assert.Equal(0, _repository.GetSizeBytes());
		Assert.Null(_repository.Get<string>(CacheType.ChartBars, "a"));
	}

	[Fact]
	public void Size_of_an_empty_cache_is_zero() => Assert.Equal(0, _repository.GetSizeBytes());
}
