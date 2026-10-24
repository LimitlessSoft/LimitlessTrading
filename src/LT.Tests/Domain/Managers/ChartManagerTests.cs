using LSCore.Exceptions;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;
using LT.Contracts.Requests.Charts;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.Managers;

public class ChartManagerTests
{
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private readonly InMemoryBarCacheRepository _cache = new();
	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 23, 15, 0, 0, TimeSpan.Zero)
	);
	private readonly ChartManager _manager;

	private static readonly GetBarsRequest Request = new()
	{
		Contract = new ContractDto
		{
			ConId = 265598,
			Symbol = "AAPL",
			SecurityType = "STK",
		},
		Timeframe = ChartTimeframe.FiveMinutes,
	};

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public ChartManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new ChartManager(_ibkr, _cache, _time);
		_ibkr.HistoricalBars =
		[
			new BarDto
			{
				Time = _time.GetUtcNow(),
				Open = 1,
				High = 2,
				Low = 0.5,
				Close = 1.5,
			},
		];
	}

	[Fact]
	public async Task Fetches_and_caches_bars()
	{
		var result = await _manager.GetBarsAsync(Request, Ct);

		Assert.Single(result.Bars);
		Assert.False(result.IsStale);
		Assert.Single(_cache.Entries);
	}

	[Fact]
	public async Task Serves_fresh_cache_without_asking_ib()
	{
		await _manager.GetBarsAsync(Request, Ct);
		_time.Advance(TimeSpan.FromSeconds(30));

		await _manager.GetBarsAsync(Request, Ct);

		Assert.Equal(1, _ibkr.HistoricalCalls);
	}

	[Fact]
	public async Task Refetches_after_the_cache_expires()
	{
		await _manager.GetBarsAsync(Request, Ct);
		_time.Advance(TimeSpan.FromMinutes(2));

		await _manager.GetBarsAsync(Request, Ct);

		Assert.Equal(2, _ibkr.HistoricalCalls);
	}

	[Fact]
	public async Task Falls_back_to_stale_cache_when_ib_fails()
	{
		await _manager.GetBarsAsync(Request, Ct);
		_time.Advance(TimeSpan.FromMinutes(2));
		_ibkr.HistoricalException = new InvalidOperationException("Not connected");

		var result = await _manager.GetBarsAsync(Request, Ct);

		Assert.True(result.IsStale);
		Assert.Equal("Not connected", result.Error);
		Assert.Single(result.Bars);
	}

	[Fact]
	public async Task Without_cache_the_error_propagates()
	{
		_ibkr.HistoricalException = new InvalidOperationException("Not connected");

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			_manager.GetBarsAsync(Request, Ct)
		);
	}

	[Fact]
	public async Task Unresolved_contract_is_rejected()
	{
		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.GetBarsAsync(
				new GetBarsRequest { Contract = new ContractDto { Symbol = "AAPL" } },
				Ct
			)
		);
	}
}
