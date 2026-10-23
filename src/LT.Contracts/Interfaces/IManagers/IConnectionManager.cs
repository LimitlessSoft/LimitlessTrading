using LT.Contracts.Dtos.Connection;
using LT.Contracts.Requests.Connection;

namespace LT.Contracts.Interfaces.IManagers;

public interface IConnectionManager
{
	ConnectionStatusDto Status { get; }

	/// <summary>Raised on any thread whenever <see cref="Status"/> changes.</summary>
	event Action<ConnectionStatusDto>? StatusChanged;

	ConnectRequest GetLastConnectRequest();
	Task ConnectAsync(ConnectRequest request, CancellationToken cancellationToken = default);
	Task DisconnectAsync();
}
