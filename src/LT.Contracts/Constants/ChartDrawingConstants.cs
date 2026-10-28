namespace LT.Contracts.Constants;

public static class ChartDrawingConstants
{
	/// <summary>The drawings of every contract, in the app-data folder.</summary>
	public const string FileName = "chart-drawings.json";

	/// <summary>Drawings on one contract's charts.</summary>
	public const int MaxDrawingsPerContract = 500;

	/// <summary>Drawings across every contract.</summary>
	public const int MaxDrawings = 5000;

	/// <summary>Points of one freehand stroke (a longer stroke keeps its first ones).</summary>
	public const int MaxBrushPoints = 2000;

	public const int MaxTextLength = 500;

	/// <summary>Grab distance (DIPs) around a drawing's lines and anchors.</summary>
	public const double HitTolerance = 5;

	/// <summary>A press that moves at least this far (DIPs) before its release draws by dragging.</summary>
	public const double DragThreshold = 4;

	/// <summary>A freehand stroke keeps a point only this far (DIPs) from the one before.</summary>
	public const double BrushMinStep = 2;

	/// <summary>Fibonacci retracement levels: 0 at the second point, 1 at the first.</summary>
	public static readonly IReadOnlyList<double> FibRetracementLevels =
	[
		0,
		0.236,
		0.382,
		0.5,
		0.618,
		0.786,
		1,
		1.618,
	];

	/// <summary>Trend-based extension levels: 0 at the third point, 1 one whole move 1 → 2 on.</summary>
	public static readonly IReadOnlyList<double> FibExtensionLevels =
	[
		0,
		0.382,
		0.618,
		1,
		1.272,
		1.618,
		2,
		2.618,
	];
}
