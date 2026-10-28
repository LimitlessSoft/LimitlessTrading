using LT.Contracts.Dtos.ChartDrawings;
using LT.Contracts.Enums;
using LT.Contracts.Requests.ChartDrawings;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>
/// Drawings on the charts (trend lines, shapes, fib levels, notes…), kept per contract so every
/// chart of a symbol shows the same ones, whatever its timeframe. Saved in chart-drawings.json.
/// <para>
/// Every change is an undo step of its contract (in memory, at most
/// <c>ChartDrawingConstants.MaxUndoSteps</c>): every chart of a symbol shares one history, so an
/// undo on one chart may step back an edit made on another chart of the symbol.
/// </para>
/// </summary>
public interface IChartDrawingManager
{
	/// <summary>A drawing was added, changed or removed; raised on the changing thread.</summary>
	event Action? DrawingsChanged;

	/// <summary>The drawings of one contract (by ConId), oldest first (drawn in that order).</summary>
	IReadOnlyList<ChartDrawingDto> GetDrawings(int conId);

	/// <summary>
	/// The style a new drawing of the kind starts with: the one last given to a drawing of it
	/// (<see cref="UpdateProperties"/>), else the kind's built-in one.
	/// </summary>
	ChartDrawingStyleDto GetDefaultStyle(ChartDrawingKind kind);

	/// <summary>
	/// Adds a drawing. Refused when its points don't fit its kind, a note has no text, its style
	/// or levels are invalid, or above <c>ChartDrawingConstants.MaxDrawingsPerContract</c> /
	/// <c>MaxDrawings</c>.
	/// </summary>
	ChartDrawingDto Create(CreateChartDrawingRequest request);

	/// <summary>New points (and note) for a drawing; its kind and contract stay.</summary>
	ChartDrawingDto Update(UpdateChartDrawingRequest request);

	/// <summary>
	/// A drawing's style, lock, hide or Fibonacci levels; a new style also becomes its kind's
	/// default (<see cref="GetDefaultStyle"/>).
	/// </summary>
	ChartDrawingDto UpdateProperties(UpdateChartDrawingPropertiesRequest request);

	/// <summary>Removes the drawing; nothing when it is gone already.</summary>
	void Delete(string id);

	/// <summary>Removes every drawing of one contract; returns how many.</summary>
	int DeleteAll(int conId);

	/// <summary>Shows every hidden drawing of one contract again; returns how many.</summary>
	int ShowHidden(int conId);

	bool CanUndo(int conId);

	bool CanRedo(int conId);

	/// <summary>Steps back the contract's last change; false when there is none.</summary>
	bool Undo(int conId);

	/// <summary>Redoes the contract's last undone change; false when there is none.</summary>
	bool Redo(int conId);
}
