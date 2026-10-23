using LT.Contracts.Dtos.Positions;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Requests.Connection;

namespace LT.Tests.Fakes;

public class FakeIbkrApiClient : IIbkrApiClient
{
	public bool IsConnected { get; set; }
	public string? ManagedAccounts { get; set; } = "DU111,DU222";
	public Exception? ConnectException { get; set; }
	public List<PositionDto> Positions { get; set; } = [];
	public int DisconnectCount { get; private set; }

	public Task ConnectAsync(ConnectRequest request, CancellationToken cancellationToken = default)
	{
		if (ConnectException != null)
			throw ConnectException;
		IsConnected = true;
		return Task.CompletedTask;
	}

	public Task DisconnectAsync()
	{
		IsConnected = false;
		DisconnectCount++;
		return Task.CompletedTask;
	}

	public Task<DateTimeOffset> GetServerTimeAsync(CancellationToken cancellationToken = default) =>
		Task.FromResult(new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero));

	public Task<IReadOnlyList<PositionDto>> GetPositionsAsync(
		CancellationToken cancellationToken = default
	) => Task.FromResult<IReadOnlyList<PositionDto>>(Positions);
}
