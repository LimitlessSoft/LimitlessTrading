using LT.Contracts.Dtos.Flex;
using LT.Contracts.Exceptions;
using LT.Contracts.Requests.Flex;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.Managers;

public class FlexImportManagerTests
{
	// 10:00 in New York, Monday 26 Oct 2026
	private static readonly DateTimeOffset Now = new(2026, 10, 26, 14, 0, 0, TimeSpan.Zero);
	private static readonly DateOnly Yesterday = new(2026, 10, 25);
	private const string Token = "123456789012345678901234";

	private readonly FakeTimeProvider _time = new(Now);
	private readonly FakeFlexApiClient _flex = new();
	private readonly InMemorySettingsRepository _settingsRepository = new();
	private readonly SettingsManager _settings;
	private readonly InMemoryFlexImportStateRepository _state = new();
	private readonly InMemoryAccountHistoryRepository _history = new();
	private readonly InMemoryTradeJournalRepository _journalRepository = new();
	private readonly FakeIbkrApiClient _ibkr = new();
	private readonly AccountPerformanceManager _performance;
	private readonly TradeJournalManager _journal;
	private readonly MessageLogManager _log = new();

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public FlexImportManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_settings = new SettingsManager(_settingsRepository, _ibkr);
		var accountManager = new AccountManager(_ibkr, new TestCache().Manager);
		_performance = new AccountPerformanceManager(
			new FakeConnectionManager(),
			accountManager,
			_history,
			_log,
			_time
		);
		_journal = new TradeJournalManager(_ibkr, accountManager, _journalRepository, _time);
	}

	private FlexImportManager CreateManager() =>
		new(_flex, _settings, _state, _performance, _journal, _log, _time);

	private void Configure(string tradeConfirm = "") =>
		_settings.SaveFlexSettings(
			new SaveFlexSettingsRequest
			{
				Token = Token,
				ActivityQueryId = "111",
				TradeConfirmQueryId = tradeConfirm,
			}
		);

	// Years back to 2024-10-27 have data, older ones are empty
	private static FlexStatementDto Activity(FlexStatementRequest request) =>
		request.To < new DateOnly(2024, 10, 27)
			? new FlexStatementDto { HasNavSection = true, HasTradesSection = true }
			: new FlexStatementDto
			{
				HasNavSection = true,
				HasTradesSection = true,
				Nav =
				[
					new FlexNavDto
					{
						Account = "U0000001",
						Date = request.To!.Value.AddDays(-1),
						Total = 1000,
					},
					new FlexNavDto
					{
						Account = "U0000001",
						Date = request.To!.Value,
						Total = 1010,
					},
				],
				Trades =
				[
					new FlexTradeDto
					{
						Account = "U0000001",
						TradeId = $"t{request.To:yyyyMMdd}",
						ExecId = $"e{request.To:yyyyMMdd}",
						ConId = 1,
						Side = "BUY",
						Quantity = 1,
						Price = 10,
						Time = Now.AddDays(-2),
					},
				],
			};

	[Fact]
	public async Task Nothing_happens_until_flex_is_set_up()
	{
		await CreateManager().ImportNowAsync(Ct);

		Assert.Empty(_flex.Requests);
		Assert.False(CreateManager().GetStatus().IsConfigured);
	}

	[Fact]
	public async Task The_first_import_takes_the_newest_year_then_back_fills_until_a_year_is_empty()
	{
		Configure();
		_flex.Handler = Activity;

		await CreateManager().ImportNowAsync(Ct);

		Assert.All(_flex.Requests, x => Assert.Equal(("111", Token), (x.QueryId, x.Token)));
		Assert.Equal(
			[Yesterday, new DateOnly(2025, 10, 25), new DateOnly(2024, 10, 25)],
			_flex.Requests.Select(x => x.To!.Value)
		);
		var state = _state.Saved!;
		Assert.True(state.BackfillComplete);
		Assert.Equal(Yesterday, state.CoveredTo);
		Assert.Equal(new DateOnly(2023, 10, 27), state.CoveredFrom);
		Assert.Equal(Now, state.LastActivitySuccess);
		Assert.Equal(4, _performance.GetHistory("U0000001").Count);
		Assert.Equal(2, _journalRepository.Fills.Values.Sum(x => x.Count));
	}

	[Fact]
	public async Task A_restart_the_same_day_downloads_nothing_again()
	{
		Configure();
		_flex.Handler = Activity;
		await CreateManager().ImportNowAsync(Ct);
		_flex.Requests.Clear();

		await CreateManager().ImportNowAsync(Ct);

		Assert.Empty(_flex.Requests);
	}

	[Fact]
	public async Task The_next_day_only_the_last_week_is_fetched()
	{
		Configure();
		_flex.Handler = Activity;
		await CreateManager().ImportNowAsync(Ct);
		_flex.Requests.Clear();
		_time.Advance(TimeSpan.FromDays(1));

		await CreateManager().ImportNowAsync(Ct);

		var request = Assert.Single(_flex.Requests);
		Assert.Equal(
			(new DateOnly(2026, 10, 18), new DateOnly(2026, 10, 26)),
			(request.From!.Value, request.To!.Value)
		);
	}

	[Fact]
	public async Task An_account_opening_date_ends_the_back_fill()
	{
		Configure();
		_flex.Handler = request => new FlexStatementDto
		{
			HasNavSection = true,
			Nav =
			[
				new FlexNavDto
				{
					Account = "U0000001",
					Date = request.To!.Value,
					Total = 1,
				},
			],
			AccountsOpened = new Dictionary<string, DateOnly> { ["U0000001"] = new(2026, 3, 1) },
		};

		await CreateManager().ImportNowAsync(Ct);

		Assert.Equal(2, _flex.Requests.Count);
		Assert.True(_state.Saved!.BackfillComplete);
	}

	[Fact]
	public async Task Statement_not_available_ends_the_back_fill()
	{
		Configure();
		_flex.Handler = request =>
			request.To < Yesterday
				? throw new FlexServiceException("not available", 1003)
				: Activity(request);

		await CreateManager().ImportNowAsync(Ct);

		Assert.True(_state.Saved!.BackfillComplete);
		Assert.Null(_state.Saved!.LastError);
	}

	[Fact]
	public async Task An_error_is_kept_in_the_status_and_logged_once()
	{
		Configure();
		_flex.Handler = _ => throw new FlexServiceException("The Flex token has expired.", 1012);
		var manager = CreateManager();

		await manager.ImportNowAsync(Ct);
		await manager.ImportNowAsync(Ct);

		var status = manager.GetStatus();
		Assert.Equal("The Flex token has expired.", status.LastError);
		Assert.Equal(Now, status.LastErrorAt);
		Assert.Single(_log.GetAll(), x => x.Text.Contains("expired"));
	}

	[Fact]
	public async Task A_failed_daily_import_waits_before_trying_again()
	{
		Configure();
		_flex.Handler = _ => throw new FlexServiceException("busy", 1009);
		var manager = CreateManager();
		await manager.ImportNowAsync(Ct);
		_flex.Requests.Clear();
		_flex.Handler = Activity;

		using var stop = new CancellationTokenSource();
		var run = manager.RunAsync(stop.Token);
		await Task.Delay(50, Ct);
		Assert.Empty(_flex.Requests);

		_time.Advance(TimeSpan.FromHours(1));
		await WaitUntilAsync(() => _state.Saved?.LastActivitySuccess != null);
		await stop.CancelAsync();
		await run;
	}

	[Fact]
	public async Task A_query_without_the_nav_section_is_reported()
	{
		Configure();
		_flex.Handler = _ => new FlexStatementDto { HasTradesSection = true };
		var manager = CreateManager();

		await manager.ImportNowAsync(Ct);

		Assert.Contains("Net Asset Value", manager.GetStatus().LastError);
	}

	[Fact]
	public async Task Trade_confirmations_import_todays_trades_on_demand_and_on_schedule()
	{
		Configure(tradeConfirm: "222");
		_flex.Handler = request =>
			request.QueryId == "222"
				? new FlexStatementDto
				{
					HasTradesSection = true,
					Trades =
					[
						new FlexTradeDto
						{
							Account = "U0000001",
							TradeId = "7001",
							ExecId = "x1",
							ConId = 1,
							Side = "BUY",
							Quantity = 5,
							Price = 12,
							Time = Now.AddMinutes(-20),
						},
					],
				}
				: new FlexStatementDto { HasNavSection = true };
		var manager = CreateManager();

		await manager.ImportNowAsync(Ct);

		var request = _flex.Requests.Single(x => x.QueryId == "222");
		Assert.Null(request.From);
		Assert.Single(_journalRepository.Fills[new DateOnly(2026, 10, 26)]);
		Assert.Equal(Now, manager.GetStatus().LastTradeConfirmImport);

		_flex.Requests.Clear();
		using var stop = new CancellationTokenSource();
		var run = manager.RunAsync(stop.Token);
		await Task.Delay(50, Ct);
		Assert.Empty(_flex.Requests);
		_time.Advance(TimeSpan.FromMinutes(5));
		await WaitUntilAsync(() => _flex.Requests.Any(x => x.QueryId == "222"));
		await stop.CancelAsync();
		await run;
	}

	[Fact]
	public async Task Another_activity_query_starts_over()
	{
		Configure();
		_flex.Handler = Activity;
		await CreateManager().ImportNowAsync(Ct);
		_flex.Requests.Clear();

		_settings.SaveFlexSettings(
			new SaveFlexSettingsRequest { Token = Token, ActivityQueryId = "333" }
		);
		await CreateManager().ImportNowAsync(Ct);

		Assert.Equal("333", _flex.Requests[0].QueryId);
		Assert.Equal(Yesterday, _flex.Requests[0].To);
	}

	[Fact]
	public async Task Saving_settings_wakes_the_scheduler()
	{
		var manager = CreateManager();
		_flex.Handler = Activity;
		using var stop = new CancellationTokenSource();
		var run = manager.RunAsync(stop.Token);
		await Task.Delay(50, Ct);

		Configure();

		await WaitUntilAsync(() => _state.Saved?.BackfillComplete == true);
		await stop.CancelAsync();
		await run;
	}

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		for (var i = 0; i < 200 && !condition(); i++)
			await Task.Delay(10, TestContext.Current.CancellationToken);
		Assert.True(condition());
	}
}
