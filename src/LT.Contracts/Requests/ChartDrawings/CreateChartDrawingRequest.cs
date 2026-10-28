using LT.Contracts.Dtos.ChartDrawings;
using LT.Contracts.Enums;

namespace LT.Contracts.Requests.ChartDrawings;

/// <summary>A new drawing on the charts of a contract.</summary>
public class CreateChartDrawingRequest
{
	public int ConId { get; set; }
	public ChartDrawingKind Kind { get; set; }
	public List<ChartDrawingPointDto> Points { get; set; } = [];

	/// <summary>Required for <see cref="ChartDrawingKind.Text"/>, ignored for the others.</summary>
	public string? Text { get; set; }

	/// <summary>Null: the style last used for the kind (<c>IChartDrawingManager.GetDefaultStyle</c>).</summary>
	public ChartDrawingStyleDto? Style { get; set; }

	/// <summary>A Fibonacci drawing's levels (a pasted one's); null: the defaults. Ignored for the others.</summary>
	public List<ChartDrawingFibLevelDto>? FibLevels { get; set; }
}
