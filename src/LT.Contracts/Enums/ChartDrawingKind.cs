namespace LT.Contracts.Enums;

/// <summary>
/// What a chart drawing is. Saved in chart-drawings.json as a number: add values at the end only.
/// </summary>
public enum ChartDrawingKind
{
	/// <summary>A segment between two points.</summary>
	TrendLine,

	/// <summary>From the first point through the second, on to the plot's edge.</summary>
	Ray,

	/// <summary>Through both points, to the plot's edges both ways.</summary>
	ExtendedLine,

	/// <summary>One price across the whole plot (one point).</summary>
	HorizontalLine,

	/// <summary>One price from its point to the plot's right edge (one point).</summary>
	HorizontalRay,

	/// <summary>One time from the top of the plot to its bottom (one point).</summary>
	VerticalLine,

	/// <summary>A segment with an arrow head at the second point.</summary>
	Arrow,

	/// <summary>A trend line (points 1–2) and its parallel through the third point.</summary>
	ParallelChannel,

	/// <summary>A box between two opposite corners.</summary>
	Rectangle,

	/// <summary>The ellipse inside the box between two opposite corners.</summary>
	Ellipse,

	/// <summary>Three corners.</summary>
	Triangle,

	/// <summary>Fibonacci retracement levels between the first point (1) and the second (0).</summary>
	FibRetracement,

	/// <summary>Trend-based Fibonacci extension: the move 1 → 2 projected from point 3.</summary>
	FibExtension,

	/// <summary>Measures the price change (and %) between two points.</summary>
	PriceRange,

	/// <summary>Measures the bars and time between two points.</summary>
	DateRange,

	/// <summary>A note at one point.</summary>
	Text,

	/// <summary>A freehand stroke (two points or more).</summary>
	Brush,
}
