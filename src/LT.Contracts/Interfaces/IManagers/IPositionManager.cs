using LT.Contracts.Dtos.Positions;

namespace LT.Contracts.Interfaces.IManagers;

public interface IPositionManager
{
	Task<IReadOnlyList<PositionDto>> GetPositionsAsync(
		CancellationToken cancellationToken = default
	);
}
