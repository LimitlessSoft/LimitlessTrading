using LSCore.Exceptions;
using LT.Contracts.Dtos.Connection;
using LT.Contracts.Enums;
using LT.Contracts.Requests.Connection;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.Managers;

public class ConnectionManagerTests
{
	private readonly FakeIbkrApiClient _ibkr = new();
	private readonly InMemorySettingsRepository _settings = new();
	private readonly MessageLogManager _messages = new();
	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero)
	);
	private readonly ConnectionManager _manager;

	private static readonly ConnectRequest ValidRequest = new()
	{
		Host = "10.0.0.5",
		Port = 4002,
		ClientId = 7,
	};

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public ConnectionManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new ConnectionManager(_ibkr, _settings, _messages, _time);
	}

	[Fact]
	public async Task Connect_goes_through_connecting_to_connected_with_accounts()
	{
		var states = new List<ConnectionStatusDto>();
		_manager.StatusChanged += states.Add;

		await _manager.ConnectAsync(ValidRequest, Ct);

		Assert.Equal(
			[ConnectionState.Connecting, ConnectionState.Connected],
			states.Select(x => x.State)
		);
		Assert.Equal(["DU111", "DU222"], _manager.Status.Accounts);
		Assert.NotNull(_manager.Status.ServerTime);
	}

	[Fact]
	public async Task Connect_applies_the_market_data_type_from_settings()
	{
		_settings.Settings.General.MarketDataType = MarketDataType.Live;

		await _manager.ConnectAsync(ValidRequest, Ct);

		Assert.Equal(MarketDataType.Live, _ibkr.MarketDataType);
	}

	[Fact]
	public async Task Connect_remembers_the_endpoint()
	{
		await _manager.ConnectAsync(ValidRequest, Ct);

		var last = _manager.GetLastConnectRequest();
		Assert.Equal(("10.0.0.5", 4002, 7), (last.Host, last.Port, last.ClientId));
	}

	[Fact]
	public async Task Connect_failure_sets_failed_state_and_logs()
	{
		_ibkr.ConnectException = new TimeoutException("no nextValidId");

		await _manager.ConnectAsync(ValidRequest, Ct);

		Assert.Equal(ConnectionState.Failed, _manager.Status.State);
		Assert.Equal("no nextValidId", _manager.Status.Error);
		Assert.Equal(1, _ibkr.DisconnectCount);
		Assert.Contains(_messages.GetAll(), x => x.Severity == MessageSeverity.Error);
	}

	[Fact]
	public async Task Invalid_request_throws_and_does_not_connect()
	{
		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.ConnectAsync(new ConnectRequest { Host = "", Port = 7497 }, Ct)
		);
		Assert.False(_ibkr.IsConnected);
		Assert.Equal(ConnectionState.Disconnected, _manager.Status.State);
	}

	[Fact]
	public async Task Connect_while_connected_throws()
	{
		await _manager.ConnectAsync(ValidRequest, Ct);

		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.ConnectAsync(ValidRequest, Ct)
		);
	}

	[Fact]
	public async Task Disconnect_returns_to_disconnected()
	{
		await _manager.ConnectAsync(ValidRequest, Ct);

		await _manager.DisconnectAsync();

		Assert.Equal(ConnectionState.Disconnected, _manager.Status.State);
		Assert.False(_ibkr.IsConnected);
	}

	[Fact]
	public async Task Healthy_heartbeat_keeps_the_connection()
	{
		await _manager.ConnectAsync(ValidRequest, Ct);

		await _manager.CheckHealthAsync(Ct);

		Assert.Equal(ConnectionState.Connected, _manager.Status.State);
	}

	[Fact]
	public async Task Lost_connection_starts_reconnecting_with_backoff()
	{
		await _manager.ConnectAsync(ValidRequest, Ct);
		_ibkr.IsConnected = false;

		await _manager.CheckHealthAsync(Ct);

		Assert.Equal(ConnectionState.Reconnecting, _manager.Status.State);
		Assert.Equal(_time.GetUtcNow() + TimeSpan.FromSeconds(2), _manager.Status.NextAttemptAt);
		Assert.Contains(_messages.GetAll(), x => x.Severity == MessageSeverity.Warning);
	}

	[Fact]
	public async Task Failed_heartbeat_is_a_lost_connection()
	{
		await _manager.ConnectAsync(ValidRequest, Ct);
		_ibkr.ServerTimeException = new TimeoutException();

		await _manager.CheckHealthAsync(Ct);

		Assert.Equal(ConnectionState.Reconnecting, _manager.Status.State);
	}

	[Fact]
	public async Task Lost_connection_without_auto_reconnect_fails()
	{
		_settings.Settings.General.AutoReconnect = false;
		await _manager.ConnectAsync(ValidRequest, Ct);
		_ibkr.IsConnected = false;

		await _manager.CheckHealthAsync(Ct);

		Assert.Equal(ConnectionState.Failed, _manager.Status.State);
	}

	[Fact]
	public async Task Reconnect_waits_for_the_backoff_then_connects()
	{
		await _manager.ConnectAsync(ValidRequest, Ct);
		_ibkr.IsConnected = false;
		await _manager.CheckHealthAsync(Ct);
		var connects = _ibkr.ConnectCount;

		await _manager.CheckHealthAsync(Ct);
		Assert.Equal(connects, _ibkr.ConnectCount);

		_time.Advance(TimeSpan.FromSeconds(3));
		await _manager.CheckHealthAsync(Ct);

		Assert.Equal(ConnectionState.Connected, _manager.Status.State);
		Assert.Equal(connects + 1, _ibkr.ConnectCount);
	}

	[Fact]
	public async Task Failed_reconnect_increases_the_attempt_and_delay()
	{
		await _manager.ConnectAsync(ValidRequest, Ct);
		_ibkr.IsConnected = false;
		await _manager.CheckHealthAsync(Ct);
		_ibkr.ConnectException = new TimeoutException("still down");

		_time.Advance(TimeSpan.FromSeconds(3));
		await _manager.CheckHealthAsync(Ct);

		Assert.Equal(ConnectionState.Reconnecting, _manager.Status.State);
		Assert.Equal(1, _manager.Status.ReconnectAttempt);
		Assert.Equal(_time.GetUtcNow() + TimeSpan.FromSeconds(5), _manager.Status.NextAttemptAt);
		Assert.Equal("still down", _manager.Status.Error);
	}

	[Fact]
	public async Task User_disconnect_stops_reconnecting()
	{
		await _manager.ConnectAsync(ValidRequest, Ct);
		_ibkr.IsConnected = false;
		await _manager.CheckHealthAsync(Ct);

		await _manager.DisconnectAsync();
		_time.Advance(TimeSpan.FromMinutes(5));
		var connects = _ibkr.ConnectCount;
		await _manager.CheckHealthAsync(Ct);

		Assert.Equal(ConnectionState.Disconnected, _manager.Status.State);
		Assert.Equal(connects, _ibkr.ConnectCount);
	}

	[Fact]
	public async Task Shutdown_disconnects()
	{
		await _manager.ConnectAsync(ValidRequest, Ct);

		await _manager.ShutdownAsync();

		Assert.Equal(ConnectionState.Disconnected, _manager.Status.State);
		Assert.False(_ibkr.IsConnected);
	}
}
