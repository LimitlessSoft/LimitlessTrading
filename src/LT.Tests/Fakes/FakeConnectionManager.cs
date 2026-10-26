using LT.Contracts.Dtos.Connection;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Connection;

namespace LT.Tests.Fakes;

/// <summary>A connection whose status the test sets; <see cref="SetStatus"/> raises StatusChanged.</summary>
public class FakeConnectionManager : IConnectionManager
{
	public ConnectionStatusDto Status { get; private set; } =
		new() { State = ConnectionState.Disconnected };

	public event Action<ConnectionStatusDto>? StatusChanged;

	public void SetStatus(ConnectionState state, params string[] accounts)
	{
		Status = new ConnectionStatusDto { State = state, Accounts = accounts };
		StatusChanged?.Invoke(Status);
	}

	public ConnectRequest GetLastConnectRequest() => new();

	public Task ConnectAsync(
		ConnectRequest request,
		CancellationToken cancellationToken = default
	) => Task.CompletedTask;

	public Task DisconnectAsync() => Task.CompletedTask;

	public Task ShutdownAsync() => Task.CompletedTask;

	public Task CheckHealthAsync(CancellationToken cancellationToken = default) =>
		Task.CompletedTask;
}
