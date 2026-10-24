using LT.Contracts.Dtos.Connection;
using LT.Contracts.Requests.Connection;

namespace LT.Contracts.Interfaces.IManagers;

public interface IConnectionManager
{
	ConnectionStatusDto Status { get; }

	/// <summary>Raised on any thread whenever <see cref="Status"/> changes.</summary>
	event Action<ConnectionStatusDto>? StatusChanged;

	ConnectRequest GetLastConnectRequest();

	/// <summary>Called by IGatewayManager once the Gateway it started has logged in.</summary>
	Task ConnectAsync(ConnectRequest request, CancellationToken cancellationToken = default);

	/// <summary>User log-out: disconnects and stops auto-reconnect.</summary>
	Task DisconnectAsync();

	/// <summary>App exit / Gateway gone: disconnects quietly and stops auto-reconnect.</summary>
	Task ShutdownAsync();

	/// <summary>
	/// One watchdog tick: heartbeats a live connection and, when it is lost and auto-reconnect is
	/// on, retries with backoff. Called periodically by the app.
	/// </summary>
	Task CheckHealthAsync(CancellationToken cancellationToken = default);
}
