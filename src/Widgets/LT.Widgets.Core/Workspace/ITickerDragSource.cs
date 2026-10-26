using LT.Contracts.Dtos.Contracts;

namespace LT.App.Workspace;

/// <summary>
/// A widget whose rows can be dragged onto a chart (see <c>TickerDragDrop.IsSource</c>): the view
/// model says which contract a row carries.
/// </summary>
public interface ITickerDragSource
{
	/// <summary>The contract the row's item carries, or null when the row can't be dragged.</summary>
	ContractDto? GetDragContract(object? item);
}
