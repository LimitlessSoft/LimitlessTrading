using LT.Contracts.Dtos.Cache;
using LT.Contracts.Dtos.Positions;

namespace LT.Contracts.Interfaces.IManagers;

public interface IPositionManager
{
	Task<IReadOnlyList<PositionDto>> GetPositionsAsync(
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// All positions first, then every change (quantity 0 = closed). Keeps each account's
	/// last-known positions as a snapshot.
	/// </summary>
	IAsyncEnumerable<PositionDto> SubscribePositionsAsync(
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// The account's last-known positions (always IsStale), for display only while live data is
	/// unavailable; never for decisions. Null when there is none or snapshots are off.
	/// </summary>
	CachedDto<IReadOnlyList<PositionDto>>? GetPositionsSnapshot(string account);

	IAsyncEnumerable<PositionPnlDto> SubscribePnlAsync(
		string account,
		int contractId,
		CancellationToken cancellationToken = default
	);
}
