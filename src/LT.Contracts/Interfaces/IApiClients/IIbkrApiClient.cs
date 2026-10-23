using LT.Contracts.Dtos.Positions;
using LT.Contracts.Requests.Connection;

namespace LT.Contracts.Interfaces.IApiClients;

/// <summary>
/// Boundary to Interactive Brokers (TWS / IB Gateway socket API). Implemented in LT.Ibkr; the only
/// place where TwsApi / IBApi types exist. Everything crossing this interface is an LT DTO.
/// </summary>
public interface IIbkrApiClient
{
	bool IsConnected { get; }
	string? ManagedAccounts { get; }
	Task ConnectAsync(ConnectRequest request, CancellationToken cancellationToken = default);
	Task DisconnectAsync();
	Task<DateTimeOffset> GetServerTimeAsync(CancellationToken cancellationToken = default);
	Task<IReadOnlyList<PositionDto>> GetPositionsAsync(
		CancellationToken cancellationToken = default
	);
}
