using LT.Contracts.Dtos.ChartDrawings;
using LT.Contracts.Requests.ChartDrawings;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>
/// Drawings on the charts (trend lines, shapes, fib levels, notes…), kept per contract so every
/// chart of a symbol shows the same ones, whatever its timeframe. Saved in chart-drawings.json.
/// </summary>
public interface IChartDrawingManager
{
	/// <summary>A drawing was added, changed or removed; raised on the changing thread.</summary>
	event Action? DrawingsChanged;

	/// <summary>The drawings of one contract (by ConId), oldest first (drawn in that order).</summary>
	IReadOnlyList<ChartDrawingDto> GetDrawings(int conId);

	/// <summary>
	/// Adds a drawing. Refused when its points don't fit its kind, a note has no text, or above
	/// <c>ChartDrawingConstants.MaxDrawingsPerContract</c> / <c>MaxDrawings</c>.
	/// </summary>
	ChartDrawingDto Create(CreateChartDrawingRequest request);

	/// <summary>New points (and note) for a drawing; its kind and contract stay.</summary>
	ChartDrawingDto Update(UpdateChartDrawingRequest request);

	/// <summary>Removes the drawing; nothing when it is gone already.</summary>
	void Delete(string id);

	/// <summary>Removes every drawing of one contract; returns how many.</summary>
	int DeleteAll(int conId);
}
