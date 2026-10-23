using LSCore.Mapper.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Positions;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Requests.Connection;
using TwsApi;

namespace LT.Ibkr;

/// <summary>
/// IIbkrApiClient over TwsApi. Holds the one live ITwsClient; registered as a singleton.
/// </summary>
public class IbkrApiClient(ITwsClientFactory twsClientFactory) : IIbkrApiClient
{
	private ITwsClient? _client;

	public bool IsConnected => _client?.IsConnected == true;

	public string? ManagedAccounts => _client?.ManagedAccounts;

	public async Task ConnectAsync(
		ConnectRequest request,
		CancellationToken cancellationToken = default
	)
	{
		await DisconnectAsync();
		_client = await twsClientFactory.ConnectAsync(
			new TwsConnectionOptions
			{
				Host = request.Host,
				Port = request.Port,
				ClientId = request.ClientId,
				ConnectTimeout = ConnectionConstants.ConnectTimeout,
			},
			cancellationToken
		);
	}

	public async Task DisconnectAsync()
	{
		var client = Interlocked.Exchange(ref _client, null);
		if (client != null)
			await client.DisposeAsync();
	}

	public Task<DateTimeOffset> GetServerTimeAsync(CancellationToken cancellationToken = default) =>
		RequireClient().GetServerTimeAsync(cancellationToken);

	public async Task<IReadOnlyList<PositionDto>> GetPositionsAsync(
		CancellationToken cancellationToken = default
	)
	{
		var positions = await RequireClient().GetPositionsAsync(cancellationToken);
		return positions.ToMappedList<PositionInfo, PositionDto>();
	}

	private ITwsClient RequireClient() =>
		_client ?? throw new InvalidOperationException("Not connected to TWS / IB Gateway.");
}
