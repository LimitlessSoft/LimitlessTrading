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
	private readonly InMemoryCacheRepository _cache = new();
	private readonly InMemorySettingsRepository _settings = new();
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
		_manager = new ChartManager(_ibkr, new CacheManager(_cache, _settings, _time), _time);
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
	public async Task Disabled_bar_cache_is_bypassed()
	{
		_settings.Settings.General.CacheChartBars = false;

		await _manager.GetBarsAsync(Request, Ct);
		await _manager.GetBarsAsync(Request, Ct);

		Assert.Equal(2, _ibkr.HistoricalCalls);
		Assert.Empty(_cache.Entries);
	}

	[Fact]
	public async Task Master_switch_off_bypasses_the_bar_cache()
	{
		_settings.Settings.General.CacheEnabled = false;

		await _manager.GetBarsAsync(Request, Ct);
		await _manager.GetBarsAsync(Request, Ct);

		Assert.Equal(2, _ibkr.HistoricalCalls);
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

	[Fact]
	public async Task Auto_period_asks_for_the_timeframe_duration()
	{
		await _manager.GetBarsAsync(Request, Ct);

		Assert.Equal(["5 D"], _ibkr.HistoricalDurations);
	}

	[Fact]
	public async Task Period_asks_for_its_span_and_drops_older_bars()
	{
		var now = _time.GetUtcNow();
		_ibkr.HistoricalBars =
		[
			new BarDto { Time = now.AddDays(-3) },
			new BarDto { Time = now.AddHours(-2) },
			new BarDto { Time = now },
		];

		var result = await _manager.GetBarsAsync(WithPeriod(ChartPeriod.Today), Ct);

		Assert.Equal(["2 D"], _ibkr.HistoricalDurations);
		Assert.Equal([now.AddHours(-2), now], result.Bars.Select(x => x.Time));
		Assert.Equal(new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero), result.From);
		Assert.Null(result.LimitedTo);
	}

	[Fact]
	public async Task Too_long_a_period_is_cut_and_reported()
	{
		var result = await _manager.GetBarsAsync(WithPeriod(ChartPeriod.OneYear), Ct);

		Assert.Equal(ChartPeriod.OneMonth, result.LimitedTo);
		Assert.Equal(_time.GetUtcNow().AddMonths(-1), result.From);
		Assert.Equal(["32 D"], _ibkr.HistoricalDurations);
	}

	[Fact]
	public async Task Periods_with_other_durations_are_cached_apart()
	{
		await _manager.GetBarsAsync(Request, Ct);
		await _manager.GetBarsAsync(WithPeriod(ChartPeriod.Today), Ct);
		await _manager.GetBarsAsync(WithPeriod(ChartPeriod.OneDay), Ct);

		// Today and 1 day are both "2 D": the second one comes from the cache
		Assert.Equal(2, _ibkr.HistoricalCalls);
		Assert.Equal(2, _cache.Entries.Count);
	}

	private static GetBarsRequest WithPeriod(ChartPeriod period) =>
		new()
		{
			Contract = Request.Contract,
			Timeframe = Request.Timeframe,
			Period = period,
		};
}
