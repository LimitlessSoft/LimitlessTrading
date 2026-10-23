using LT.Contracts.Dtos.Positions;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;

namespace LT.Domain.Managers;

public class PositionManager(IIbkrApiClient ibkrApiClient) : IPositionManager
{
	public async Task<IReadOnlyList<PositionDto>> GetPositionsAsync(
		CancellationToken cancellationToken = default
	)
	{
		if (!ibkrApiClient.IsConnected)
			return [];

		var positions = await ibkrApiClient.GetPositionsAsync(cancellationToken);
		return positions
			.Where(x => x.Quantity != 0)
			.OrderBy(x => x.Account)
			.ThenBy(x => x.Symbol)
			.ToList();
	}
}
