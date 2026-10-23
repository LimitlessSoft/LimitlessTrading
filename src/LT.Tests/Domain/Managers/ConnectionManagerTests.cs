using LSCore.Exceptions;
using LT.Contracts.Dtos.Connection;
using LT.Contracts.Enums;
using LT.Contracts.Requests.Connection;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

public class ConnectionManagerTests
{
	private readonly FakeIbkrApiClient _ibkr = new();
	private readonly InMemorySettingsRepository _settings = new();
	private readonly ConnectionManager _manager;

	private static readonly ConnectRequest ValidRequest = new()
	{
		Host = "10.0.0.5",
		Port = 4002,
		ClientId = 7,
	};

	public ConnectionManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new ConnectionManager(_ibkr, _settings);
	}

	[Fact]
	public async Task Connect_goes_through_connecting_to_connected_with_accounts()
	{
		var states = new List<ConnectionStatusDto>();
		_manager.StatusChanged += states.Add;

		await _manager.ConnectAsync(ValidRequest, TestContext.Current.CancellationToken);

		Assert.Equal(
			[ConnectionState.Connecting, ConnectionState.Connected],
			states.Select(x => x.State)
		);
		Assert.Equal(["DU111", "DU222"], _manager.Status.Accounts);
		Assert.NotNull(_manager.Status.ServerTime);
	}

	[Fact]
	public async Task Connect_remembers_the_endpoint()
	{
		await _manager.ConnectAsync(ValidRequest, TestContext.Current.CancellationToken);

		var last = _manager.GetLastConnectRequest();
		Assert.Equal(("10.0.0.5", 4002, 7), (last.Host, last.Port, last.ClientId));
	}

	[Fact]
	public async Task Connect_failure_sets_failed_state_with_error()
	{
		_ibkr.ConnectException = new TimeoutException("no nextValidId");

		await _manager.ConnectAsync(ValidRequest, TestContext.Current.CancellationToken);

		Assert.Equal(ConnectionState.Failed, _manager.Status.State);
		Assert.Equal("no nextValidId", _manager.Status.Error);
		Assert.Equal(1, _ibkr.DisconnectCount);
	}

	[Fact]
	public async Task Invalid_request_throws_and_does_not_connect()
	{
		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.ConnectAsync(
				new ConnectRequest { Host = "", Port = 7497 },
				TestContext.Current.CancellationToken
			)
		);
		Assert.False(_ibkr.IsConnected);
		Assert.Equal(ConnectionState.Disconnected, _manager.Status.State);
	}

	[Fact]
	public async Task Connect_while_connected_throws()
	{
		await _manager.ConnectAsync(ValidRequest, TestContext.Current.CancellationToken);

		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.ConnectAsync(ValidRequest, TestContext.Current.CancellationToken)
		);
	}

	[Fact]
	public async Task Disconnect_returns_to_disconnected()
	{
		await _manager.ConnectAsync(ValidRequest, TestContext.Current.CancellationToken);

		await _manager.DisconnectAsync();

		Assert.Equal(ConnectionState.Disconnected, _manager.Status.State);
		Assert.False(_ibkr.IsConnected);
	}
}
