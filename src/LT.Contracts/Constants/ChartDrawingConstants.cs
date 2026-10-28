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

	/// <summary>The colour of a drawing that was never restyled (a note's text is lighter).</summary>
	public const string DefaultColor = "#2962FF";

	/// <summary>A note's text colour until restyled.</summary>
	public const string DefaultTextColor = "#E6E8EB";

	public const double DefaultLineWidth = 1.5;
	public const double MinLineWidth = 1;
	public const double MaxLineWidth = 4;

	/// <summary>The widths the style bar offers.</summary>
	public static readonly IReadOnlyList<double> LineWidths = [1, 1.5, 2, 3, 4];

	/// <summary>The swatches the style bar offers (any hex colour can be typed too).</summary>
	public static readonly IReadOnlyList<string> Colors =
	[
		"#2962FF",
		"#00BCD4",
		"#089981",
		"#4CAF50",
		"#FFEB3B",
		"#FF9800",
		"#F23645",
		"#E91E63",
		"#9C27B0",
		"#787B86",
		"#E6E8EB",
		"#131722",
	];

	/// <summary>Levels of one Fibonacci drawing.</summary>
	public const int MaxFibLevels = 24;

	public const double MinFibLevel = -10;
	public const double MaxFibLevel = 10;

	/// <summary>Undo steps kept per contract (in memory; the oldest go first).</summary>
	public const int MaxUndoSteps = 100;

	/// <summary>A pasted or duplicated drawing sits this many bars right of the one copied…</summary>
	public const double PasteOffsetBars = 3;

	/// <summary>…and this many DIPs lower on the price chart.</summary>
	public const double PasteOffsetDips = 16;

	/// <summary>The default levels' colours, by level index (TradingView-like).</summary>
	public static readonly IReadOnlyList<string> FibColors =
	[
		"#787B86",
		"#F23645",
		"#FF9800",
		"#4CAF50",
		"#089981",
		"#00BCD4",
		"#787B86",
		"#2962FF",
	];

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
