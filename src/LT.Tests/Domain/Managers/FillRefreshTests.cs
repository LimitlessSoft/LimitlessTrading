using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.Orders;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.Managers;

/// <summary>
/// An order of the app reporting a fill makes the recording trade journal ask for executions
/// right away (coalesced), so the fill sound doesn't wait for the 30 s poll. Wired the way
/// FillAlertService does it, over the fake IB client (no order reaches IB).
/// </summary>
public class FillRefreshTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 26, 14, 0, 0, TimeSpan.Zero);
	private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(100);

	private readonly FakeIbkrApiClient _ibkr = new()
	{
		IsConnected = true,
		ManagedAccounts = "DU1",
	};
	private readonly TestCache _cache = new();
	private readonly FakeTimeProvider _time = new(Now);
	private readonly FakeSoundPlayer _sound = new();
	private readonly TradeJournalManager _journal;
	private readonly OrderManager _orders;
	private readonly FillAlertManager _alerts;
	private int _ordersFilled;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public FillRefreshTests()
	{
		_journal = new TradeJournalManager(
			_ibkr,
			new AccountManager(_ibkr, _cache.Manager),
			new InMemoryTradeJournalRepository(),
			_time
		);
		_orders = new OrderManager(_ibkr, _cache.Manager);
		_alerts = new FillAlertManager(new SettingsManager(_cache.Settings, _ibkr), _sound);
		_journal.ExecutionsReceived += x => _alerts.OnExecutions(x);
		_orders.OrderFilled += _ =>
		{
			_ordersFilled++;
			_journal.RequestExecutionsRefresh();
		};
	}

	[Fact]
	public async Task A_fill_status_asks_for_executions_at_once_and_plays_the_sound()
	{
		using var session = new CancellationTokenSource();
		var recording = await StartRecordingAsync(session.Token);

		_ibkr.Executions = [Execution("e1")];
		await PlaceAndFollowAsync(Status("Submitted", 0), Status("Filled", 1, done: true));
		var waited = await AdvanceUntilAsync(() => _ibkr.ExecutionCalls == 2);

		Assert.InRange(waited, TradeJournalConstants.FillRefreshDelay, TimeSpan.FromSeconds(5));
		Assert.Equal(1, _sound.FillsPlayed);
		await StopAsync(session, recording);
	}

	[Fact]
	public async Task A_burst_of_partial_fills_makes_one_request()
	{
		using var session = new CancellationTokenSource();
		var recording = await StartRecordingAsync(session.Token);

		_ibkr.Executions = [Execution("e1.01"), Execution("e1.02"), Execution("e1.03")];
		await PlaceAndFollowAsync(
			Status("Submitted", 0),
			Status("PreSubmitted", 1),
			Status("Submitted", 2),
			Status("Submitted", 3),
			Status("Filled", 3, done: true),
			Status("Filled", 3, done: true)
		);
		await AdvanceUntilAsync(() => _ibkr.ExecutionCalls == 2);
		await AdvanceAsync(TimeSpan.FromSeconds(10));

		Assert.Equal(3, _ordersFilled);
		Assert.Equal(2, _ibkr.ExecutionCalls);
		Assert.Equal(1, _sound.FillsPlayed);
		await StopAsync(session, recording);
	}

	[Fact]
	public async Task Fills_reported_while_a_request_runs_make_one_more_request()
	{
		using var session = new CancellationTokenSource();
		var recording = await StartRecordingAsync(session.Token);
		// More fills reported while the second request is under way
		_journal.ExecutionsReceived += _ =>
		{
			if (_ibkr.ExecutionCalls != 2)
				return;
			for (var i = 0; i < 5; i++)
				_journal.RequestExecutionsRefresh();
		};

		await PlaceAndFollowAsync(Status("Filled", 1, done: true));
		await AdvanceUntilAsync(() => _ibkr.ExecutionCalls == 3);
		await AdvanceAsync(TimeSpan.FromSeconds(10));

		Assert.Equal(3, _ibkr.ExecutionCalls);
		await StopAsync(session, recording);
	}

	[Fact]
	public async Task Statuses_without_a_fill_ask_for_nothing()
	{
		using var session = new CancellationTokenSource();
		var recording = await StartRecordingAsync(session.Token);

		await PlaceAndFollowAsync(
			Status("PreSubmitted", 0),
			Status("Submitted", 0),
			Status("Cancelled", 0, done: true)
		);
		await AdvanceAsync(TimeSpan.FromSeconds(10));

		Assert.Equal(0, _ordersFilled);
		Assert.Equal(1, _ibkr.ExecutionCalls);
		await StopAsync(session, recording);
	}

	[Fact]
	public void A_refresh_asked_for_while_not_recording_is_dropped()
	{
		_journal.RequestExecutionsRefresh();

		Assert.Equal(0, _ibkr.ExecutionCalls);
	}

	[Theory]
	[InlineData("Submitted", 0, 0, false, false)]
	[InlineData("Cancelled", 0, 0, false, false)]
	[InlineData("Submitted", 1, 0, false, true)]
	[InlineData("Submitted", 2, 1, false, true)]
	[InlineData("Filled", 0, 0, false, true)]
	[InlineData("Filled", 3, 3, false, false)]
	[InlineData("Filled", 0, 0, true, false)]
	[InlineData("Cancelled", 2, 2, false, false)]
	public void A_new_fill_is_more_filled_quantity_or_a_first_filled_status(
		string status,
		int filled,
		int previousFilled,
		bool filledSeen,
		bool expected
	) =>
		Assert.Equal(
			expected,
			OrderHelpers.IsNewFill(Status(status, filled), previousFilled, filledSeen)
		);

	// Recording with IB's replay (empty) already answered
	private async Task<Task> StartRecordingAsync(CancellationToken session)
	{
		_alerts.BeginSession();
		var recording = _journal.RecordAsync(session);
		await WaitUntilAsync(() => _ibkr.ExecutionCalls == 1);
		return recording;
	}

	private static async Task StopAsync(CancellationTokenSource session, Task recording)
	{
		await session.CancelAsync();
		await recording;
	}

	private async Task PlaceAndFollowAsync(params OrderStatusDto[] statuses)
	{
		_ibkr.OrderStatuses = [.. statuses];
		var placed = await _orders.PlaceOrderAsync(OneShare(), Ct);
		await placed.Statuses.ToListAsync(Ct);
	}

	// Moves the fake clock in small steps, giving the recording loop time to reach its timers,
	// until the condition holds; returns how far the clock moved
	private async Task<TimeSpan> AdvanceUntilAsync(Func<bool> condition)
	{
		var advanced = TimeSpan.Zero;
		while (!condition() && advanced < AccountConstants.ExecutionsRefreshInterval)
		{
			await Task.Delay(5, Ct);
			_time.Advance(Step);
			advanced += Step;
		}
		await WaitUntilAsync(condition);
		return advanced;
	}

	private async Task AdvanceAsync(TimeSpan duration)
	{
		for (var advanced = TimeSpan.Zero; advanced < duration; advanced += Step)
		{
			await Task.Delay(5, Ct);
			_time.Advance(Step);
		}
		await Task.Delay(50, Ct);
	}

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		for (var i = 0; i < 200 && !condition(); i++)
			await Task.Delay(10, Ct);
		Assert.True(condition());
	}

	private static OrderStatusDto Status(string status, decimal filled, bool done = false) =>
		new()
		{
			OrderId = 100,
			Status = status,
			Filled = filled,
			IsDone = done,
		};

	private static PlaceOrderRequest OneShare() =>
		new()
		{
			Contract = new ContractDto
			{
				ConId = 265598,
				Symbol = "AAPL",
				SecurityType = "STK",
				Exchange = "SMART",
				Currency = "USD",
			},
			Account = "DU1",
			Action = OrderAction.Buy,
			OrderType = OrderType.Market,
			Quantity = 1,
			TimeInForce = TimeInForce.Day,
		};

	private static ExecutionDto Execution(string execId) =>
		new()
		{
			ExecId = execId,
			Time = Now,
			Account = "DU1",
			ConId = 265598,
			Symbol = "AAPL",
			SecurityType = "STK",
			Side = "BOT",
			Shares = 1,
			Price = 10,
		};
}
