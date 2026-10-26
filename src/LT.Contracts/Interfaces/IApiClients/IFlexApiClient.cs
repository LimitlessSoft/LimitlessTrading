using LT.Contracts.Dtos.Flex;
using LT.Contracts.Requests.Flex;

namespace LT.Contracts.Interfaces.IApiClients;

/// <summary>
/// IB's Flex Web Service (v3): SendRequest, then GetStatement until the statement is generated.
/// Requests are serialized and spaced within IB's per-token limits (one per second, ten per
/// minute). Throws <see cref="Exceptions.FlexServiceException"/> with a message ready to show.
/// </summary>
public interface IFlexApiClient
{
	Task<FlexStatementDto> GetStatementAsync(
		FlexStatementRequest request,
		CancellationToken cancellationToken = default
	);
}
