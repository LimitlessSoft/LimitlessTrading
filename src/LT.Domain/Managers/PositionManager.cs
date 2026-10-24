using System.Runtime.CompilerServices;
using LT.Contracts.Dtos.Positions;
using LT.Contracts.Helpers;
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

	public IAsyncEnumerable<PositionDto> SubscribePositionsAsync(
		CancellationToken cancellationToken = default
	) => ibkrApiClient.SubscribePositionsAsync(cancellationToken);

	public async IAsyncEnumerable<PositionPnlDto> SubscribePnlAsync(
		string account,
		int contractId,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		await foreach (
			var pnl in ibkrApiClient.SubscribePositionPnlAsync(
				account,
				contractId,
				cancellationToken
			)
		)
			yield return new PositionPnlDto
			{
				Position = pnl.Position,
				DailyPnl = IbValueHelpers.Clean(pnl.DailyPnl),
				UnrealizedPnl = IbValueHelpers.Clean(pnl.UnrealizedPnl),
				RealizedPnl = IbValueHelpers.Clean(pnl.RealizedPnl),
				MarketValue = IbValueHelpers.Clean(pnl.MarketValue),
			};
	}
}
