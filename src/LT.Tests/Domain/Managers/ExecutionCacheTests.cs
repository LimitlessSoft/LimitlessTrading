using LT.Contracts.Constants;
using LT.Contracts.Dtos.Executions;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

public class ExecutionCacheTests
{
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private readonly TestCache _cache = new();

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public ExecutionCacheTests() =>
		_ibkr.Executions = [
			new ExecutionDto { ExecId = "a", Account = "DU1" },
			new ExecutionDto { ExecId = "b", Account = "DU2" },
		];

	private ExecutionManager Manager() => new(_ibkr, _cache.Manager, _cache.Time);

	[Fact]
	public async Task Fresh_fills_are_served_without_asking_ib()
	{
		await Manager().GetExecutionsAsync("DU1", Ct);
		_cache.Time.Advance(TimeSpan.FromSeconds(5));

		var result = await Manager().GetExecutionsAsync("DU1", Ct);

		Assert.Equal(1, _ibkr.ExecutionCalls);
		Assert.False(result.IsStale);
		Assert.Equal(["a"], result.Value.Select(x => x.ExecId));
	}

	[Fact]
	public async Task Fills_are_asked_again_after_the_short_ttl()
	{
		await Manager().GetExecutionsAsync("DU1", Ct);
		_cache.Time.Advance(CacheConstants.ExecutionsTtl);

		await Manager().GetExecutionsAsync("DU1", Ct);

		Assert.Equal(2, _ibkr.ExecutionCalls);
	}

	[Fact]
	public async Task Ib_failing_serves_the_cached_fills_as_stale()
	{
		await Manager().GetExecutionsAsync("DU1", Ct);
		_cache.Time.Advance(TimeSpan.FromMinutes(5));
		_ibkr.ExecutionsException = new InvalidOperationException("Timed out");

		var result = await Manager().GetExecutionsAsync("DU1", Ct);

		Assert.True(result.IsStale);
		Assert.Equal("Timed out", result.Error);
		Assert.Equal(["a"], result.Value.Select(x => x.ExecId));
	}

	[Fact]
	public async Task Disconnected_serves_only_that_accounts_cached_fills()
	{
		await Manager().GetExecutionsAsync("DU1", Ct);
		_ibkr.IsConnected = false;

		var own = await Manager().GetExecutionsAsync("DU1", Ct);
		var other = await Manager().GetExecutionsAsync("DU2", Ct);
		var none = await Manager().GetExecutionsAsync(null, Ct);

		Assert.True(own.IsStale);
		Assert.Equal(["a"], own.Value.Select(x => x.ExecId));
		Assert.Empty(other.Value);
		Assert.False(other.IsStale);
		Assert.Empty(none.Value);
	}

	[Fact]
	public async Task Disabled_executions_cache_always_asks_ib()
	{
		_cache.Settings.Settings.General.CacheExecutions = false;

		await Manager().GetExecutionsAsync("DU1", Ct);
		await Manager().GetExecutionsAsync("DU1", Ct);
		_ibkr.IsConnected = false;
		var offline = await Manager().GetExecutionsAsync("DU1", Ct);

		Assert.Equal(2, _ibkr.ExecutionCalls);
		Assert.Empty(offline.Value);
		Assert.Empty(_cache.Repository.Entries);
	}
}
