using LT.Contracts.Dtos.Contracts;

namespace LT.App.Workspace;

/// <summary>
/// A widget that takes a contract dropped on it (see <c>TickerDragDrop.IsTarget</c>), e.g. a
/// chart showing the ticker dragged from a watchlist row.
/// </summary>
public interface ITickerDropTarget
{
	void DropContract(ContractDto contract);
}
