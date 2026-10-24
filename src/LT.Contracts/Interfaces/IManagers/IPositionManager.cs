using LT.Contracts.Dtos.Positions;

namespace LT.Contracts.Interfaces.IManagers;

public interface IPositionManager
{
	Task<IReadOnlyList<PositionDto>> GetPositionsAsync(
		CancellationToken cancellationToken = default
	);

	/// <summary>All positions first, then every change (quantity 0 = closed).</summary>
	IAsyncEnumerable<PositionDto> SubscribePositionsAsync(
		CancellationToken cancellationToken = default
	);

	IAsyncEnumerable<PositionPnlDto> SubscribePnlAsync(
		string account,
		int contractId,
		CancellationToken cancellationToken = default
	);
}
