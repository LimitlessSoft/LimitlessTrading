using LT.Contracts.Constants;
using LT.Contracts.Dtos.Account;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Dtos.Flex;
using LT.Contracts.Entities;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.Managers;

public class TradeJournalManagerTests
{
	// 10:00 in New York
	private static readonly DateTimeOffset Now = new(2026, 10, 26, 14, 0, 0, TimeSpan.Zero);
	private static readonly DateOnly Today = new(2026, 10, 26);
	private static readonly DateOnly Yesterday = new(2026, 10, 23);

	private readonly FakeIbkrApiClient _ibkr = new()
	{
		IsConnected = true,
		ManagedAccounts = "DU1",
	};
	private readonly InMemoryTradeJournalRepository _repository = new();
	private readonly FakeTimeProvider _time = new(Now);
	private readonly TradeJournalManager _manager;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public TradeJournalManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new TradeJournalManager(
			_ibkr,
			new AccountManager(_ibkr, new TestCache().Manager),
			_repository,
			_time
		);
	}

	[Fact]
	public void Today_is_the_new_york_trading_day() => Assert.Equal(Today, _manager.Today);

	[Fact]
	public async Task Executions_are_saved_per_day_and_deduplicated()
	{
		_ibkr.Executions = [Execution("e1", "BOT", 0), Execution("e2", "SLD", 5)];

		Assert.Equal(2, await _manager.RecordExecutionsAsync(Ct));
		Assert.Equal(0, await _manager.RecordExecutionsAsync(Ct));

		Assert.Equal(2, _repository.Fills[Today].Count);
		Assert.All(_repository.Fills[Today], x => Assert.Equal(Now, x.RecordedAt));
	}

	[Fact]
	public async Task A_commission_reported_later_updates_the_saved_fill()
	{
		_ibkr.Executions = [Execution("e1", "BOT", 0)];
		await _manager.RecordExecutionsAsync(Ct);
		_ibkr.Executions = [Execution("e1", "BOT", 0, commission: 1.5)];

		Assert.Equal(1, await _manager.RecordExecutionsAsync(Ct));
		Assert.Equal(1.5, Assert.Single(_repository.Fills[Today]).Commission);
	}

	[Fact]
	public async Task Unsupported_executions_are_reported_once_and_cleared_when_they_work()
	{
		var changes = 0;
		_manager.FillsChanged += () => changes++;
		_ibkr.ExecutionsException = new NotSupportedException("Executions are not supported yet");

		await _manager.RecordExecutionsAsync(Ct);
		await _manager.RecordExecutionsAsync(Ct);

		Assert.Equal("Executions are not supported yet", _manager.ExecutionsError);
		Assert.Equal(1, changes);

		_ibkr.ExecutionsException = null;
		await _manager.RecordExecutionsAsync(Ct);

		Assert.Null(_manager.ExecutionsError);
		Assert.Equal(2, changes);
	}

	[Fact]
	public async Task Nothing_is_asked_while_disconnected()
	{
		_ibkr.IsConnected = false;

		Assert.Equal(0, await _manager.RecordExecutionsAsync(Ct));
		Assert.Equal(0, _ibkr.ExecutionCalls);
	}

	[Fact]
	public async Task Day_performance_has_the_trades_closed_that_day()
	{
		_ibkr.Executions =
		[
			Execution("e1", "BOT", 0, price: 10, commission: 1),
			Execution("e2", "SLD", 5, price: 11, commission: 1),
			Execution("e3", "SLD", 10, price: 20),
			Execution("e4", "BOT", 20, price: 21),
			Execution("e5", "BOT", 30, price: 5, conId: 9),
		];
		await _manager.RecordExecutionsAsync(Ct);

		var day = _manager.GetDayPerformance(Today, "DU1");

		Assert.Equal(2, day.Trades.Count);
		Assert.Equal(1, day.OpenTrades);
		Assert.Equal(5, day.FillCount);
		Assert.Equal(-2, day.Stats.NetPnl, 6); // +98 long, -100 short
		Assert.Equal(0.5, day.Stats.WinRate);
		Assert.Empty(_manager.GetDayPerformance(Today, "DU2").Trades);
	}

	[Fact]
	public void A_position_carried_overnight_closes_against_its_recorded_entry()
	{
		_repository.Fills[Yesterday] =
		[
			new FillEntity
			{
				ExecId = "y1",
				Time = Now.AddDays(-3),
				Account = "DU1",
				ConId = 1,
				Symbol = "AAPL",
				Side = "BOT",
				Shares = 10,
				Price = 100,
			},
		];
		_repository.Fills[Today] =
		[
			new FillEntity
			{
				ExecId = "t1",
				Time = Now,
				Account = "DU1",
				ConId = 1,
				Symbol = "AAPL",
				Side = "SLD",
				Shares = 10,
				Price = 103,
			},
		];

		var today = _manager.GetDayPerformance(Today, null);
		var yesterday = _manager.GetDayPerformance(Yesterday, null);

		Assert.Equal(30, Assert.Single(today.Trades).GrossPnl, 6);
		Assert.Empty(yesterday.Trades);
		Assert.Equal(1, yesterday.OpenTrades);
	}

	[Fact]
	public void Contract_trades_span_every_recorded_day_of_that_contract_and_account()
	{
		_repository.Fills[Yesterday] =
		[
			new FillEntity
			{
				ExecId = "y1",
				Time = Now.AddDays(-3),
				Account = "DU1",
				ConId = 1,
				Symbol = "AAPL",
				Side = "BOT",
				Shares = 10,
				Price = 100,
			},
		];
		_repository.Fills[Today] =
		[
			new FillEntity
			{
				ExecId = "t1",
				Time = Now,
				Account = "DU1",
				ConId = 1,
				Symbol = "AAPL",
				Side = "SLD",
				Shares = 10,
				Price = 90,
			},
			new FillEntity
			{
				ExecId = "t2",
				Time = Now,
				Account = "DU1",
				ConId = 2,
				Symbol = "MSFT",
				Side = "BOT",
				Shares = 1,
				Price = 400,
			},
			new FillEntity
			{
				ExecId = "t3",
				Time = Now.AddMinutes(1),
				Account = "DU2",
				ConId = 1,
				Symbol = "AAPL",
				Side = "BOT",
				Shares = 5,
				Price = 91,
			},
		];

		var trades = _manager.GetContractTrades(1, "DU1");

		Assert.Equal(["y1", "t1"], trades.Fills.Select(x => x.ExecId));
		var leg = Assert.Single(trades.Legs);
		Assert.Equal("y1", leg.EntryExecId);
		Assert.Equal("t1", leg.ExitExecId);
		Assert.Equal(-100, leg.NetPnl, 6);
		Assert.Equal(3, _manager.GetContractTrades(1, null).Fills.Count);
		Assert.Empty(_manager.GetContractTrades(3, null).Fills);
	}

	[Fact]
	public void Pnl_is_sampled_and_saved_at_most_every_save_interval()
	{
		_manager.RecordPnl("DU1", new AccountPnlDto { DailyPnl = 10 });
		_time.Advance(TimeSpan.FromSeconds(3));
		_manager.RecordPnl("DU1", new AccountPnlDto { DailyPnl = 12 });
		_time.Advance(TimeSpan.FromSeconds(10));
		_manager.RecordPnl("DU1", new AccountPnlDto { DailyPnl = -5 });
		_manager.RecordPnl("DU1", new AccountPnlDto { DailyPnl = null });

		Assert.Equal([12d, -5d], _manager.GetPnlCurve(Today, "DU1").Select(x => x.Value));
		Assert.Equal(0, _repository.PnlSaves);

		_time.Advance(TradeJournalConstants.PnlSaveInterval);
		_manager.RecordPnl("DU1", new AccountPnlDto { DailyPnl = 7 });

		Assert.Equal(1, _repository.PnlSaves);
		Assert.Equal(3, _repository.PnlSamples[Today].Count);
	}

	[Fact]
	public void Flush_saves_pending_points_and_a_restart_continues_the_curve()
	{
		_manager.RecordPnl("DU1", new AccountPnlDto { DailyPnl = 10 });
		_manager.Flush();

		var restarted = new TradeJournalManager(
			_ibkr,
			new AccountManager(_ibkr, new TestCache().Manager),
			_repository,
			_time
		);
		_time.Advance(TimeSpan.FromMinutes(1));
		restarted.RecordPnl("DU1", new AccountPnlDto { DailyPnl = 20 });

		Assert.Equal([10d, 20d], restarted.GetPnlCurve(Today, "DU1").Select(x => x.Value));
	}

	[Fact]
	public void Days_include_today_and_every_recorded_day()
	{
		_repository.Fills[Yesterday] = [];
		_repository.PnlSamples[new DateOnly(2026, 10, 1)] = [];

		Assert.Equal([new DateOnly(2026, 10, 1), Yesterday, Today], _manager.GetDays());
	}

	[Fact]
	public async Task Recording_follows_pnl_and_executions_until_cancelled()
	{
		_ibkr.AccountPnlSource = (_, _) =>
			new[] { new AccountPnlDto { DailyPnl = 42 } }.ToAsyncEnumerable();
		_ibkr.Executions = [Execution("e1", "BOT", 0)];
		using var session = new CancellationTokenSource();

		var recording = _manager.RecordAsync(session.Token);
		await WaitUntilAsync(() => _ibkr.ExecutionCalls == 1 && _repository.Fills.Count == 1);
		await WaitUntilAsync(() => _manager.GetPnlCurve(Today, "DU1").Count == 1);
		_time.Advance(AccountConstants.ExecutionsRefreshInterval);
		await WaitUntilAsync(() => _ibkr.ExecutionCalls == 2);
		await session.CancelAsync();
		await recording;

		Assert.Equal(42, Assert.Single(_repository.PnlSamples[Today]).DailyPnl);
	}

	[Fact]
	public void Flex_trades_give_stats_while_ib_executions_fail()
	{
		_ibkr.ExecutionsException = new NotSupportedException("Executions are not supported yet");

		var changed = _manager.ImportFlexTrades(
			[
				FlexTrade("t1", "e1", "BUY", 0, 10, commission: 1),
				FlexTrade("t2", "e2", "SELL", 5, 11, commission: 1),
			],
			[]
		);

		Assert.Equal(2, changed);
		var day = _manager.GetDayPerformance(Today, "DU1");
		var trade = Assert.Single(day.Trades);
		Assert.Equal(100, trade.GrossPnl, 6);
		Assert.Equal(2, trade.Commission, 6);
		Assert.Contains(Today, _manager.GetDays());
	}

	[Fact]
	public async Task A_flex_trade_and_the_same_socket_fill_stay_one_fill()
	{
		_ibkr.Executions = [Execution("e1", "BOT", 0)];
		await _manager.RecordExecutionsAsync(Ct);

		var changed = _manager.ImportFlexTrades(
			[FlexTrade("t1", "e1", "BUY", 0, 10, commission: 1.25)],
			[]
		);

		Assert.Equal(1, changed);
		var fill = Assert.Single(_repository.Fills[Today]);
		Assert.Equal(("t1", 1.25), (fill.TradeId, fill.Commission!.Value));
	}

	[Fact]
	public void Imported_history_reaches_every_day_with_fills()
	{
		_manager.ImportFlexTrades(
			[
				FlexTrade("t1", "e1", "BUY", -60 * 24 * 30, 10),
				FlexTrade("t2", "e2", "SELL", -60 * 24 * 3, 12),
			],
			[]
		);

		var days = _manager.GetDays();
		Assert.Contains(new DateOnly(2026, 9, 26), days);
		Assert.Contains(Yesterday, days);
		Assert.Single(_manager.GetDayPerformance(Yesterday, null).Trades);
	}

	[Fact]
	public void A_cancelled_flex_trade_is_removed_from_the_journal()
	{
		_manager.ImportFlexTrades([FlexTrade("t1", "e1", "BUY", 0, 10)], []);

		Assert.Equal(1, _manager.ImportFlexTrades([], ["t1"]));
		Assert.Empty(_repository.Fills[Today]);
	}

	[Fact]
	public void A_flex_trade_without_time_lands_on_its_trade_date()
	{
		var trade = FlexTrade("t1", "", "BUY", 0, 10);
		trade.Time = null;
		trade.TradeDate = Yesterday;

		_manager.ImportFlexTrades([trade], []);

		var fill = Assert.Single(_repository.Fills[Yesterday]);
		Assert.Equal("flex:t1", fill.ExecId);
	}

	private static FlexTradeDto FlexTrade(
		string tradeId,
		string execId,
		string side,
		int minute,
		double price,
		double? commission = null
	) =>
		new()
		{
			Account = "DU1",
			TradeId = tradeId,
			ExecId = execId,
			ConId = 1,
			Symbol = "AAPL",
			AssetCategory = "STK",
			Multiplier = 1,
			Side = side,
			Quantity = 100,
			Price = price,
			Commission = commission,
			Time = Now.AddMinutes(minute),
		};

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		for (var i = 0; i < 200 && !condition(); i++)
			await Task.Delay(10, Ct);
		Assert.True(condition());
	}

	private static ExecutionDto Execution(
		string execId,
		string side,
		int minute,
		double price = 10,
		double? commission = null,
		int conId = 1
	) =>
		new()
		{
			ExecId = execId,
			Time = Now.AddMinutes(minute),
			Account = "DU1",
			ConId = conId,
			Symbol = "AAPL",
			SecurityType = "STK",
			Side = side,
			Shares = 100,
			Price = price,
			Commission = commission,
		};
}
