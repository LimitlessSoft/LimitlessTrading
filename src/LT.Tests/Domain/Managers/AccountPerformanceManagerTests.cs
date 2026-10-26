using System.Threading.Channels;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Account;
using LT.Contracts.Dtos.Flex;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.Managers;

public class AccountPerformanceManagerTests
{
	// 15:00 UTC = 11:00 in New York, Monday 26 Oct 2026
	private static readonly DateOnly Today = new(2026, 10, 26);

	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 26, 15, 0, 0, TimeSpan.Zero)
	);
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private readonly FakeConnectionManager _connection = new();
	private readonly InMemoryAccountHistoryRepository _repository = new();
	private readonly Channel<AccountPnlDto> _pnl = Channel.CreateUnbounded<AccountPnlDto>();
	private readonly AccountPerformanceManager _manager;

	public AccountPerformanceManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_ibkr.AccountValues =
		[
			new AccountValueDto
			{
				Account = "DU1",
				Tag = AccountConstants.NetLiquidation,
				Value = "100000",
				Currency = "USD",
			},
		];
		_ibkr.AccountPnlSource = (account, ct) =>
			account == "DU1"
				? _pnl.Reader.ReadAllAsync(ct)
				: AsyncEnumerable.Empty<AccountPnlDto>();
		_manager = new AccountPerformanceManager(
			_connection,
			new AccountManager(_ibkr, new TestCache().Manager),
			_repository,
			new MessageLogManager(),
			_time
		);
	}

	[Fact]
	public async Task Records_net_liquidation_and_pnl_into_today_and_saves_on_stop()
	{
		_connection.SetStatus(ConnectionState.Connected, "DU1");
		using var stop = new CancellationTokenSource();
		var recording = _manager.RecordAsync(stop.Token);

		await WritePnlAsync(250, 40, 210);
		await stop.CancelAsync();
		await recording;

		var day = Assert.Single(_repository.Entries["DU1"].Days);
		Assert.Equal(Today, day.Date);
		Assert.Equal((100000m, "USD"), (day.NetLiquidation, day.Currency));
		Assert.Equal(
			(250.0, 40.0, 210.0),
			(day.DailyPnl!.Value, day.RealizedPnl!.Value, day.UnrealizedPnl!.Value)
		);
	}

	[Fact]
	public async Task Disk_writes_are_throttled()
	{
		_connection.SetStatus(ConnectionState.Connected, "DU1");
		using var stop = new CancellationTokenSource();
		var recording = _manager.RecordAsync(stop.Token);

		await WritePnlAsync(100);
		await WritePnlAsync(120);
		Assert.Equal(0, _repository.SaveCount);

		_time.Advance(AccountPerformanceConstants.SaveInterval);
		await WaitUntilAsync(() => _repository.SaveCount == 1);
		Assert.Equal(120, _repository.Entries["DU1"].Days[0].DailyPnl);

		await stop.CancelAsync();
		await recording;
	}

	[Fact]
	public async Task History_includes_today_live_before_it_is_saved()
	{
		_repository.Save(
			new AccountHistoryEntity
			{
				Account = "DU1",
				Days = [new AccountDaySnapshotEntity { Date = Today.AddDays(-3), DailyPnl = -5 }],
			}
		);
		_connection.SetStatus(ConnectionState.Connected, "DU1");
		using var stop = new CancellationTokenSource();
		var recording = _manager.RecordAsync(stop.Token);
		var changed = new List<string>();
		_manager.HistoryChanged += changed.Add;

		await WritePnlAsync(75);

		var history = _manager.GetHistory("DU1");
		Assert.Equal([Today.AddDays(-3), Today], history.Select(x => x.Date));
		Assert.Equal(75, history[^1].DailyPnl);
		Assert.Contains("DU1", changed);

		await stop.CancelAsync();
		await recording;
	}

	[Fact]
	public async Task A_new_trading_day_gets_its_own_snapshot()
	{
		_connection.SetStatus(ConnectionState.Connected, "DU1");
		using var stop = new CancellationTokenSource();
		var recording = _manager.RecordAsync(stop.Token);

		await WritePnlAsync(100);
		_time.Advance(TimeSpan.FromDays(1));
		await WritePnlAsync(-30);

		var history = _manager.GetHistory("DU1");
		Assert.Equal([Today, Today.AddDays(1)], history.Select(x => x.Date));
		Assert.Equal([100.0, -30.0], history.Select(x => x.DailyPnl!.Value));
		// The summary only comes when it changes; the new day starts from the last one
		Assert.Equal(100000m, history[1].NetLiquidation);

		await stop.CancelAsync();
		await recording;
	}

	[Fact]
	public async Task A_disconnect_saves_and_nothing_is_recorded_while_disconnected()
	{
		_connection.SetStatus(ConnectionState.Connected, "DU1");
		using var stop = new CancellationTokenSource();
		var recording = _manager.RecordAsync(stop.Token);
		await WritePnlAsync(100);

		_connection.SetStatus(ConnectionState.Disconnected);

		Assert.Equal(1, _repository.SaveCount);
		Assert.Equal(100, _repository.Entries["DU1"].Days[0].DailyPnl);
		await stop.CancelAsync();
		await recording;
	}

	[Fact]
	public void Today_is_the_New_York_date()
	{
		_time.SetUtcNow(new DateTimeOffset(2026, 10, 27, 2, 0, 0, TimeSpan.Zero));

		Assert.Equal(Today, _manager.GetToday());
	}

	private async Task WritePnlAsync(
		double daily,
		double? realized = null,
		double? unrealized = null
	)
	{
		var ct = TestContext.Current.CancellationToken;
		await _pnl.Writer.WriteAsync(
			new AccountPnlDto
			{
				DailyPnl = daily,
				RealizedPnl = realized,
				UnrealizedPnl = unrealized,
			},
			ct
		);
		await WaitUntilAsync(() =>
			_manager.GetHistory("DU1").LastOrDefault() is { } day
			&& day.DailyPnl == daily
			&& day.NetLiquidation != null
			&& day.Date == _manager.GetToday()
		);
	}

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		var deadline = DateTime.UtcNow.AddSeconds(5);
		while (!condition())
		{
			Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the recorder");
			await Task.Delay(10, TestContext.Current.CancellationToken);
		}
	}

	[Fact]
	public void Flex_history_is_merged_saved_and_announced_without_touching_today()
	{
		var changed = new List<string>();
		_manager.HistoryChanged += changed.Add;

		var count = _manager.ImportFlexHistory(
			[
				new FlexNavDto
				{
					Account = "U0000001",
					Date = new DateOnly(2026, 10, 22),
					Total = 1000,
				},
				new FlexNavDto
				{
					Account = "U0000001",
					Date = new DateOnly(2026, 10, 23),
					Total = 1600,
				},
				new FlexNavDto
				{
					Account = "U0000001",
					Date = Today,
					Total = 9,
				},
			],
			[
				new FlexCashFlowDto
				{
					Account = "U0000001",
					Date = new DateOnly(2026, 10, 23),
					Amount = 500,
				},
			]
		);

		Assert.Equal(2, count);
		Assert.Equal(["U0000001"], changed);
		var days = _manager.GetHistory("U0000001");
		Assert.Equal(2, days.Count);
		Assert.Equal(
			(100.0, 500.0, AccountDaySource.Flex),
			(days[1].DailyPnl!.Value, days[1].CashFlow!.Value, days[1].Source)
		);
		Assert.Equal(2, _repository.Entries["U0000001"].Days.Count);
	}
}
