using System.Runtime.CompilerServices;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Cache;
using LT.Contracts.Dtos.Positions;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;

namespace LT.Domain.Managers;

public class PositionManager(IIbkrApiClient ibkrApiClient, ICacheManager cacheManager)
	: IPositionManager
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

	public async IAsyncEnumerable<PositionDto> SubscribePositionsAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		// What this stream has said so far; the changed account's open positions are its snapshot
		var current = new Dictionary<(string Account, int ContractId), PositionDto>();
		await foreach (var position in ibkrApiClient.SubscribePositionsAsync(cancellationToken))
		{
			if (position.Quantity == 0)
				current.Remove((position.Account, position.ContractId));
			else
				current[(position.Account, position.ContractId)] = position;
			cacheManager.Save<IReadOnlyList<PositionDto>>(
				CacheType.Snapshots,
				CacheHelpers.GetAccountKey(CacheConstants.PositionsSnapshot, position.Account),
				current.Values.Where(x => x.Account == position.Account).ToList()
			);
			yield return position;
		}
	}

	public CachedDto<IReadOnlyList<PositionDto>>? GetPositionsSnapshot(string account) =>
		cacheManager.GetStale<IReadOnlyList<PositionDto>>(
			CacheType.Snapshots,
			CacheHelpers.GetAccountKey(CacheConstants.PositionsSnapshot, account)
		);

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
