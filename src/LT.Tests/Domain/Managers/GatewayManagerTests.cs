using LSCore.Exceptions;
using LT.Contracts.Dtos.Gateway;
using LT.Contracts.Enums;
using LT.Contracts.Requests.Gateway;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

public class GatewayManagerTests
{
	private readonly FakeIbcGatewayProcess _process = new();
	private readonly FakeIbInstallProbe _probe = new();
	private readonly FakeIbcDownloader _downloader = new();
	private readonly FakeIbkrApiClient _ibkr = new();
	private readonly InMemorySettingsRepository _settings = new();
	private readonly MessageLogManager _messages = new();
	private readonly ConnectionManager _connection;
	private readonly GatewayManager _manager;

	private static StartGatewayRequest ValidRequest() =>
		new()
		{
			Username = "trader1",
			Password = "s3cret",
			TradingMode = TradingMode.Paper,
			IbcPath = "/opt/ibc",
			GatewayPath = "/home/u/Jts",
			GatewayVersion = "1037",
		};

	public GatewayManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_downloader.Probe = _probe;
		// Real time: the start loop polls the port once per second
		_connection = new ConnectionManager(_ibkr, _settings, _messages, TimeProvider.System);
		_manager = new GatewayManager(
			_process,
			_probe,
			_downloader,
			_connection,
			_settings,
			TimeProvider.System
		);
	}

	[Fact]
	public async Task Start_goes_to_running_and_connects_to_the_gateway_port()
	{
		var states = new List<GatewayStatusDto>();
		_manager.StatusChanged += states.Add;

		await _manager.StartAsync(ValidRequest(), TestContext.Current.CancellationToken);

		Assert.Equal([GatewayState.Starting, GatewayState.Running], states.Select(x => x.State));
		Assert.Equal(4002, _manager.Status.ApiPort);
		Assert.Equal(ConnectionState.Connected, _connection.Status.State);
		Assert.Equal(4002, _connection.GetLastConnectRequest().Port);
	}

	[Fact]
	public async Task Start_deletes_the_config_holding_the_password()
	{
		await _manager.StartAsync(ValidRequest(), TestContext.Current.CancellationToken);

		Assert.Contains("IbPassword=s3cret", _process.LastLaunch!.ConfigContent);
		Assert.True(_process.DeleteConfigCount > 0);
	}

	[Fact]
	public async Task Start_remembers_settings_but_not_the_password()
	{
		await _manager.StartAsync(ValidRequest(), TestContext.Current.CancellationToken);

		var last = _manager.GetLastStartRequest();
		Assert.Equal(("trader1", "1037"), (last.Username, last.GatewayVersion));
		Assert.Equal(string.Empty, last.Password);
	}

	[Fact]
	public async Task Second_factor_prompt_is_reported_until_port_opens()
	{
		_process.PortOpensAfterProbes = 1;
		_process.OnStart = p =>
			p.EmitOutput("IBC: detected dialog entitled: Second Factor Authentication");
		var states = new List<GatewayState>();
		_manager.StatusChanged += x => states.Add(x.State);

		await _manager.StartAsync(ValidRequest(), TestContext.Current.CancellationToken);

		Assert.Equal(
			[GatewayState.Starting, GatewayState.AwaitingSecondFactor, GatewayState.Running],
			states
		);
	}

	[Fact]
	public async Task Rejected_login_fails_and_stops_the_process()
	{
		_process.PortOpensAfterProbes = null;
		_process.OnStart = p => p.EmitOutput("IBC: Unrecognized Username or Password");

		await _manager.StartAsync(ValidRequest(), TestContext.Current.CancellationToken);

		Assert.Equal(GatewayState.Failed, _manager.Status.State);
		Assert.Contains("rejected the login", _manager.Status.Error);
		Assert.Equal(1, _process.StopCount);
		Assert.Equal(ConnectionState.Disconnected, _connection.Status.State);
	}

	[Fact]
	public async Task Ibc_exiting_during_start_fails_with_its_last_line()
	{
		_process.PortOpensAfterProbes = null;
		_process.OnStart = p =>
		{
			p.EmitOutput("Error: IB Gateway version 1037 is not installed");
			p.Exit(1);
		};

		await _manager.StartAsync(ValidRequest(), TestContext.Current.CancellationToken);

		Assert.Equal(GatewayState.Failed, _manager.Status.State);
		Assert.Contains("code 1", _manager.Status.Error);
		Assert.Contains("is not installed", _manager.Status.Error);
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
	public async Task Missing_ibc_script_throws_before_starting()
	{
		_process.ScriptPresent = false;

		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.StartAsync(ValidRequest(), TestContext.Current.CancellationToken)
		);
		Assert.Null(_process.LastLaunch);
	}

	[Fact]
	public async Task Busy_api_port_throws_before_starting()
	{
		_process.PortOpenBeforeStart = true;

		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.StartAsync(ValidRequest(), TestContext.Current.CancellationToken)
		);
		Assert.Null(_process.LastLaunch);
	}

	[Fact]
	public async Task Invalid_request_throws_and_does_not_start()
	{
		var request = ValidRequest();
		request.Password = "";

		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.StartAsync(request, TestContext.Current.CancellationToken)
		);
		Assert.Null(_process.LastLaunch);
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
	public async Task InstallIbc_installs_into_app_data_and_returns_the_ibc_folder()
	{
		var path = await _manager.InstallIbcAsync(TestContext.Current.CancellationToken);

		Assert.Equal(_downloader.InstalledTo, path);
		Assert.EndsWith("ibc", path);
	}

	[Fact]
	public async Task InstallIbc_without_an_asset_for_this_os_throws()
	{
		_downloader.Release = new IbcReleaseDto { Version = "9.9", Assets = [] };

		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.InstallIbcAsync(TestContext.Current.CancellationToken)
		);
		Assert.Null(_downloader.InstalledTo);
	}

	[Fact]
	public async Task InstallIbc_while_gateway_runs_throws()
	{
		await _manager.StartAsync(ValidRequest(), TestContext.Current.CancellationToken);

		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.InstallIbcAsync(TestContext.Current.CancellationToken)
		);
	}

	[Fact]
	public async Task Stop_disconnects_and_stops_the_process()
	{
		await _manager.StartAsync(ValidRequest(), TestContext.Current.CancellationToken);

		await _manager.StopAsync();

		Assert.Equal(GatewayState.Stopped, _manager.Status.State);
		Assert.Equal(ConnectionState.Disconnected, _connection.Status.State);
		Assert.Equal(1, _process.StopCount);
	}
}
