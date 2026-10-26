using LSCore.Exceptions;
using LT.Contracts.Dtos.Gateway;
using LT.Contracts.Enums;
using LT.Contracts.Requests.Gateway;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.Managers;

public class GatewayManagerTests
{
	private const string Launcher = "/home/u/Jts/ibgateway/1037/ibgateway";

	private readonly FakeGatewayProcess _process = new();
	private readonly FakeIbInstallProbe _probe = new FakeIbInstallProbe().AddFile(Launcher);
	private readonly FakeIbkrApiClient _ibkr = new();
	private readonly InMemorySettingsRepository _settings = new();
	private readonly MessageLogManager _messages = new();
	private readonly ConnectionManager _connection;
	private readonly GatewayManager _manager;

	private static StartGatewayRequest ValidRequest() =>
		new() { GatewayPath = "/home/u/Jts", GatewayVersion = "1037" };

	public GatewayManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		// Real time: the start loop polls the port once per second
		_connection = new ConnectionManager(_ibkr, _settings, _messages, TimeProvider.System);
		_manager = CreateManager(TimeProvider.System);
	}

	private GatewayManager CreateManager(TimeProvider timeProvider) =>
		new(_process, _probe, _connection, _settings, timeProvider);

	[Fact]
	public async Task Start_launches_gateway_and_connects_once_the_user_has_logged_in()
	{
		var states = new List<GatewayStatusDto>();
		_manager.StatusChanged += states.Add;

		await _manager.StartAsync(ValidRequest(), TestContext.Current.CancellationToken);

		Assert.Equal(Launcher, _process.LastLauncher);
		Assert.Equal(
			[GatewayState.Starting, GatewayState.AwaitingLogin, GatewayState.Running],
			states.Select(x => x.State)
		);
		Assert.Equal(
			(TradingMode.Paper, 4002),
			(_manager.Status.TradingMode, _manager.Status.ApiPort)
		);
		Assert.False(_manager.Status.IsExternal);
		Assert.Equal(ConnectionState.Connected, _connection.Status.State);
		Assert.Equal(4002, _connection.GetLastConnectRequest().Port);
	}

	[Fact]
	public async Task Start_waits_while_the_user_logs_in()
	{
		_process.PortOpensAfterProbes = 1;

		await _manager.StartAsync(ValidRequest(), TestContext.Current.CancellationToken);

		Assert.Equal(GatewayState.Running, _manager.Status.State);
		Assert.Equal(ConnectionState.Connected, _connection.Status.State);
	}

	[Fact]
	public async Task Logging_in_to_live_connects_to_the_live_port_and_remembers_the_mode()
	{
		_process.PortAfterLogin = 4001;

		await _manager.StartAsync(ValidRequest(), TestContext.Current.CancellationToken);

		Assert.Equal(
			(TradingMode.Live, 4001),
			(_manager.Status.TradingMode, _manager.Status.ApiPort)
		);
		Assert.Equal(4001, _connection.GetLastConnectRequest().Port);
		Assert.Equal(TradingMode.Live, _settings.Settings.Gateway.TradingMode);
	}

	[Fact]
	public async Task Start_remembers_the_install()
	{
		var request = ValidRequest();
		request.GatewayVersion = "1050";
		_probe.AddFile("/home/u/Jts/ibgateway/1050/ibgateway1");

		await _manager.StartAsync(request, TestContext.Current.CancellationToken);

		var last = _manager.GetLastStartRequest();
		Assert.Equal(("/home/u/Jts", "1050"), (last.GatewayPath, last.GatewayVersion));
		Assert.Equal("/home/u/Jts/ibgateway/1050/ibgateway1", _process.LastLauncher);
	}

	[Fact]
	public async Task Gateway_closed_before_login_goes_back_to_stopped_without_an_error()
	{
		_process.PortOpensAfterProbes = null;
		_process.OnStart = p => p.Exit(0);

		await _manager.StartAsync(ValidRequest(), TestContext.Current.CancellationToken);

		Assert.Equal(GatewayState.Stopped, _manager.Status.State);
		Assert.Null(_manager.Status.Error);
		Assert.Equal(ConnectionState.Disconnected, _connection.Status.State);
	}

	[Fact]
	public async Task Gateway_failing_before_login_reports_its_exit_code()
	{
		_process.PortOpensAfterProbes = null;
		_process.OnStart = p => p.Exit(1);

		await _manager.StartAsync(ValidRequest(), TestContext.Current.CancellationToken);

		Assert.Equal(GatewayState.Stopped, _manager.Status.State);
		Assert.Contains("code 1", _manager.Status.Error);
	}

	[Fact]
	public async Task Stop_while_waiting_for_login_cancels_and_closes_gateway()
	{
		_process.PortOpensAfterProbes = null;
		var awaiting = new TaskCompletionSource();
		_manager.StatusChanged += x =>
		{
			if (x.State == GatewayState.AwaitingLogin)
				awaiting.TrySetResult();
		};

		var start = _manager.StartAsync(ValidRequest(), TestContext.Current.CancellationToken);
		await awaiting.Task;
		await _manager.StopAsync();
		await start;

		Assert.Equal(GatewayState.Stopped, _manager.Status.State);
		Assert.Equal(1, _process.StopCount);
		Assert.Equal(ConnectionState.Disconnected, _connection.Status.State);
	}

	[Fact]
	public async Task No_login_within_the_timeout_fails_and_closes_gateway()
	{
		var time = new FakeTimeProvider();
		var manager = CreateManager(time);
		_process.PortOpensAfterProbes = null;

		var start = manager.StartAsync(ValidRequest(), TestContext.Current.CancellationToken);
		while (!start.IsCompleted)
		{
			time.Advance(TimeSpan.FromMinutes(1));
			await Task.Delay(1, TestContext.Current.CancellationToken);
		}
		await start;

		Assert.Equal(GatewayState.Failed, manager.Status.State);
		Assert.Contains("4001", manager.Status.Error);
		Assert.Equal(1, _process.StopCount);
	}

	[Fact]
	public async Task Crash_after_login_is_reported()
	{
		await _manager.StartAsync(ValidRequest(), TestContext.Current.CancellationToken);

		_process.Exit(137);

		Assert.Equal(GatewayState.Failed, _manager.Status.State);
		Assert.Contains("137", _manager.Status.Error);
	}

	[Fact]
	public async Task Missing_launcher_throws_before_starting()
	{
		var request = ValidRequest();
		request.GatewayVersion = "1099";

		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.StartAsync(request, TestContext.Current.CancellationToken)
		);
		Assert.Null(_process.LastLauncher);
	}

	[Fact]
	public async Task Busy_api_port_uses_the_running_gateway_instead_of_starting_one()
	{
		_process.PortsOpenBeforeStart.Add(4001);

		await _manager.StartAsync(ValidRequest(), TestContext.Current.CancellationToken);

		Assert.Null(_process.LastLauncher);
		Assert.Equal(
			(GatewayState.Running, true, 4001),
			(_manager.Status.State, _manager.Status.IsExternal, _manager.Status.ApiPort)
		);
		Assert.Equal(ConnectionState.Connected, _connection.Status.State);
	}

	[Theory]
	[InlineData(4001, TradingMode.Live)]
	[InlineData(4002, TradingMode.Paper)]
	public async Task Attach_connects_to_a_gateway_running_on_either_port(
		int port,
		TradingMode mode
	)
	{
		_process.PortsOpenBeforeStart.Add(port);

		var attached = await _manager.AttachRunningAsync(TestContext.Current.CancellationToken);

		Assert.True(attached);
		Assert.Equal(
			(GatewayState.Running, true, port, mode),
			(
				_manager.Status.State,
				_manager.Status.IsExternal,
				_manager.Status.ApiPort,
				_manager.Status.TradingMode
			)
		);
		Assert.Equal(ConnectionState.Connected, _connection.Status.State);
		Assert.Null(_process.LastLauncher);
	}

	[Fact]
	public async Task Attach_prefers_the_last_used_mode()
	{
		_process.PortsOpenBeforeStart.UnionWith([4001, 4002]);
		_settings.Settings.Gateway.TradingMode = TradingMode.Live;

		await _manager.AttachRunningAsync(TestContext.Current.CancellationToken);

		Assert.Equal(4001, _manager.Status.ApiPort);
	}

	[Fact]
	public async Task Attach_without_a_running_gateway_leaves_it_stopped()
	{
		var attached = await _manager.AttachRunningAsync(TestContext.Current.CancellationToken);

		Assert.False(attached);
		Assert.Equal(GatewayState.Stopped, _manager.Status.State);
	}

	[Fact]
	public async Task Stopping_an_external_gateway_only_disconnects()
	{
		_process.PortsOpenBeforeStart.Add(4002);
		await _manager.StartAsync(ValidRequest(), TestContext.Current.CancellationToken);

		await _manager.StopAsync();

		Assert.Equal(GatewayState.Stopped, _manager.Status.State);
		Assert.Equal(ConnectionState.Disconnected, _connection.Status.State);
		Assert.Equal(0, _process.StopCount);
	}

	[Fact]
	public async Task Invalid_request_throws_and_does_not_start()
	{
		var request = ValidRequest();
		request.GatewayPath = "";

		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.StartAsync(request, TestContext.Current.CancellationToken)
		);
		Assert.Null(_process.LastLauncher);
		Assert.Equal(GatewayState.Stopped, _manager.Status.State);
	}

	[Fact]
	public async Task Start_while_running_throws()
	{
		await _manager.StartAsync(ValidRequest(), TestContext.Current.CancellationToken);

		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.StartAsync(ValidRequest(), TestContext.Current.CancellationToken)
		);
	}

	[Fact]
	public async Task Stop_disconnects_and_closes_the_gateway_it_started()
	{
		await _manager.StartAsync(ValidRequest(), TestContext.Current.CancellationToken);

		await _manager.StopAsync();

		Assert.Equal(GatewayState.Stopped, _manager.Status.State);
		Assert.Equal(ConnectionState.Disconnected, _connection.Status.State);
		Assert.Equal(1, _process.StopCount);
	}
}
