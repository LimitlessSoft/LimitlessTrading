using LSCore.Exceptions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.PriceAlerts;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IRepositories;
using LT.Contracts.Requests.PriceAlerts;
using LT.Domain.Managers;
using LT.Repository.Repositories;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.Managers;

/// <summary>
/// Price alerts: direction told at creation, fired once on the first price past the level, saved
/// across restarts, and watched over the fake IB client's quotes (no IB connection).
/// </summary>
public sealed class PriceAlertManagerTests : IDisposable
{
	private static readonly ContractDto Aapl = new()
	{
		ConId = 265598,
		Symbol = "AAPL",
		SecurityType = "STK",
		Exchange = "SMART",
		Currency = "USD",
	};
	private static readonly ContractDto Msft = new()
	{
		ConId = 272093,
		Symbol = "MSFT",
		SecurityType = "STK",
		Exchange = "SMART",
		Currency = "USD",
	};

	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 26, 14, 0, 0, TimeSpan.Zero)
	);
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private readonly MessageLogManager _log = new();
	private readonly InMemoryPriceAlertRepository _repository = new();
	private readonly PriceAlertManager _manager;
	private readonly string _directory = Path.Combine(
		Path.GetTempPath(),
		$"lt-price-alert-manager-tests-{Guid.NewGuid():N}"
	);

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public PriceAlertManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = Create(_repository);
	}

	public void Dispose()
	{
		if (Directory.Exists(_directory))
			Directory.Delete(_directory, recursive: true);
	}

	[Fact]
	public void A_level_above_the_price_alerts_on_the_way_up_and_one_below_on_the_way_down()
	{
		var up = Add(Aapl, 190, current: 185);
		var down = Add(Aapl, 180, current: 185);

		Assert.Equal(PriceAlertCondition.CrossingUp, up.Condition);
		Assert.Equal(PriceAlertCondition.CrossingDown, down.Condition);
		Assert.Equal(_time.GetUtcNow(), up.CreatedAt);
		Assert.Equal(2, _repository.Alerts.Count);
	}

	[Theory]
	[InlineData(0, 185)]
	[InlineData(double.NaN, 185)]
	[InlineData(190, 0)]
	public void An_invalid_price_or_unknown_current_price_is_refused(
		double price,
		double current
	) => Assert.Throws<LSCoreBadRequestException>(() => Add(Aapl, price, current));

	[Fact]
	public void An_alert_needs_a_resolved_contract() =>
		Assert.Throws<LSCoreBadRequestException>(() => Add(new ContractDto(), 190, 185));

	[Fact]
	public void A_price_short_of_the_level_does_not_fire()
	{
		Add(Aapl, 190, current: 185);

		Assert.Empty(_manager.OnPrice(Aapl.ConId, 189.99));
		Assert.False(_manager.GetAlerts(Aapl.ConId)[0].IsTriggered);
	}

	[Fact]
	public void An_alert_fires_once_on_the_first_price_past_its_level()
	{
		var alert = Add(Aapl, 190, current: 185);
		var triggered = new List<PriceAlertDto>();
		_manager.AlertTriggered += triggered.Add;
		_time.Advance(TimeSpan.FromMinutes(5));

		var fired = Assert.Single(_manager.OnPrice(Aapl.ConId, 190.25));
		Assert.Empty(_manager.OnPrice(Aapl.ConId, 191));
		Assert.Empty(_manager.OnPrice(Aapl.ConId, 189));
		Assert.Empty(_manager.OnPrice(Aapl.ConId, 192));

		Assert.Equal(alert.Id, fired.Id);
		Assert.Equal(alert.Id, Assert.Single(triggered).Id);
		var stored = _manager.GetAlerts(Aapl.ConId)[0];
		Assert.Equal(_time.GetUtcNow(), stored.TriggeredAt);
		Assert.Equal(190.25, stored.TriggerPrice);
		Assert.NotNull(_repository.Alerts[0].TriggeredAt);
		Assert.Contains(
			_log.GetAll(),
			x => x.Source == MessageLogConstants.SourceAlerts && x.Text.Contains("crossed above")
		);
	}

	[Fact]
	public void A_price_of_another_contract_fires_nothing()
	{
		Add(Aapl, 190, current: 185);

		Assert.Empty(_manager.OnPrice(Msft.ConId, 500));
	}

	[Fact]
	public void A_downward_alert_fires_at_or_below_its_level()
	{
		Add(Aapl, 180, current: 185);

		Assert.Empty(_manager.OnPrice(Aapl.ConId, 180.01));
		Assert.Single(_manager.OnPrice(Aapl.ConId, 180));
	}

	[Fact]
	public void Moving_an_alert_tells_its_direction_again_and_arms_a_triggered_one()
	{
		var alert = Add(Aapl, 190, current: 185);
		_manager.OnPrice(Aapl.ConId, 191);

		var moved = _manager.Move(
			new MovePriceAlertRequest
			{
				Id = alert.Id,
				Price = 188,
				CurrentPrice = 191,
			}
		);

		Assert.False(moved.IsTriggered);
		Assert.Null(moved.TriggerPrice);
		Assert.Equal(PriceAlertCondition.CrossingDown, moved.Condition);
		Assert.Empty(_manager.OnPrice(Aapl.ConId, 189));
		Assert.Single(_manager.OnPrice(Aapl.ConId, 187.5));
	}

	[Fact]
	public void Moving_a_removed_alert_is_refused() =>
		Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.Move(
				new MovePriceAlertRequest
				{
					Id = "gone",
					Price = 188,
					CurrentPrice = 191,
				}
			)
		);

	[Fact]
	public void Alerts_on_more_contracts_than_the_limit_are_refused()
	{
		for (var conId = 1; conId <= PriceAlertConstants.MaxWatchedContracts; conId++)
			Add(new ContractDto { ConId = conId, Symbol = $"S{conId}" }, 10, current: 5);

		Add(new ContractDto { ConId = 1, Symbol = "S1" }, 11, current: 5);
		Assert.Throws<LSCoreBadRequestException>(() =>
			Add(new ContractDto { ConId = 999, Symbol = "NEW" }, 10, current: 5)
		);
	}

	[Fact]
	public void Removing_the_triggered_alerts_keeps_the_active_ones()
	{
		Add(Aapl, 190, current: 185);
		var active = Add(Aapl, 200, current: 185);
		Add(Msft, 400, current: 410);
		_manager.OnPrice(Aapl.ConId, 195);
		_manager.OnPrice(Msft.ConId, 399);

		Assert.Equal(1, _manager.DeleteTriggered(Aapl.ConId));

		Assert.Equal(active.Id, Assert.Single(_manager.GetAlerts(Aapl.ConId)).Id);
		Assert.Single(_manager.GetAlerts(Msft.ConId));
	}

	[Fact]
	public void Alerts_and_their_state_survive_a_restart()
	{
		var path = Path.Combine(_directory, PriceAlertConstants.FileName);
		var before = Create(new PriceAlertRepository(path));
		var fired = Add(before, Aapl, 190, current: 185);
		var armed = Add(before, Aapl, 170, current: 185);
		before.OnPrice(Aapl.ConId, 190.5);

		var after = Create(new PriceAlertRepository(path));
		var alerts = after.GetAlerts(Aapl.ConId);

		Assert.Equal([fired.Id, armed.Id], alerts.Select(x => x.Id));
		Assert.True(alerts[0].IsTriggered);
		Assert.Equal(190.5, alerts[0].TriggerPrice);
		Assert.Equal(PriceAlertCondition.CrossingDown, alerts[1].Condition);
		Assert.False(alerts[1].IsTriggered);
		// The fired one stays fired: only the armed one fires on the way down
		Assert.Equal(armed.Id, Assert.Single(after.OnPrice(Aapl.ConId, 169)).Id);
	}

	[Fact]
	public async Task Watching_fires_alerts_from_the_contracts_quotes()
	{
		_ibkr.Ticks =
		[
			new MarketDataTickDto { Field = TickTypeHelpers.Last, Price = 189 },
			new MarketDataTickDto { Field = TickTypeHelpers.Last, Price = 190.5 },
		];
		Add(Aapl, 190, current: 185);
		var triggered = new TaskCompletionSource<PriceAlertDto>(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		_manager.AlertTriggered += x => triggered.TrySetResult(x);
		using var session = new CancellationTokenSource();

		var watching = _manager.WatchAsync(session.Token);
		var alert = await triggered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
		await session.CancelAsync();
		await watching.WaitAsync(TimeSpan.FromSeconds(10), Ct);

		Assert.Equal(190.5, alert.TriggerPrice);
	}

	[Fact]
	public async Task Contracts_without_an_active_alert_are_not_watched()
	{
		Add(Aapl, 190, current: 185);
		_manager.OnPrice(Aapl.ConId, 191);
		using var session = new CancellationTokenSource();

		var watching = _manager.WatchAsync(session.Token);
		await Task.Delay(50, Ct);
		await session.CancelAsync();
		await watching.WaitAsync(TimeSpan.FromSeconds(10), Ct);

		Assert.Empty(_ibkr.GenericTicksRequested);
	}

	[Fact]
	public async Task A_failed_stream_is_logged_and_opened_again_after_the_retry_delay()
	{
		_ibkr.IsConnected = false;
		Add(Aapl, 190, current: 185);
		var triggered = new TaskCompletionSource<PriceAlertDto>(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		_manager.AlertTriggered += x => triggered.TrySetResult(x);
		using var session = new CancellationTokenSource();

		var watching = _manager.WatchAsync(session.Token);
		await WaitUntilAsync(() =>
			_log.GetAll().Any(x => x.Severity == MessageSeverity.Warning && x.Text.Contains("AAPL"))
		);
		_ibkr.IsConnected = true;
		_ibkr.Ticks = [new MarketDataTickDto { Field = TickTypeHelpers.Last, Price = 191 }];
		Assert.False(triggered.Task.IsCompleted);
		// The retry waits on the fake clock (advanced until the stream is opened again)
		await WaitUntilAsync(() =>
		{
			_time.Advance(PriceAlertConstants.RetryDelay);
			return triggered.Task.IsCompleted;
		});

		var alert = await triggered.Task;
		await session.CancelAsync();
		await watching.WaitAsync(TimeSpan.FromSeconds(10), Ct);

		Assert.Equal(191, alert.TriggerPrice);
	}

	private PriceAlertManager Create(IPriceAlertRepository repository) =>
		new(repository, new MarketDataManager(_ibkr), _log, _time);

	private PriceAlertDto Add(ContractDto contract, double price, double current) =>
		Add(_manager, contract, price, current);

	private static PriceAlertDto Add(
		PriceAlertManager manager,
		ContractDto contract,
		double price,
		double current
	) =>
		manager.Create(
			new CreatePriceAlertRequest
			{
				Contract = contract,
				Price = price,
				CurrentPrice = current,
			}
		);

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		var deadline = DateTime.UtcNow.AddSeconds(10);
		while (!condition())
		{
			if (DateTime.UtcNow > deadline)
				throw new TimeoutException();
			await Task.Delay(10, Ct);
		}
	}
}
