namespace LT.Indicators.Sdk;

/// <summary>Where an indicator is drawn.</summary>
public enum PaneKind
{
	/// <summary>On the price chart, on the price scale.</summary>
	Overlay,

	/// <summary>In a pane of its own below the price chart, with its own scale.</summary>
	Separate,

	/// <summary>On the volume bars, on the volume scale.</summary>
	Volume,
}

public enum LineStyle
{
	Solid,
	Dashed,
	Dotted,
}

public enum MarkerShape
{
	TriangleUp,
	TriangleDown,
	Circle,
	Square,
	Cross,
}

public enum MarkerPosition
{
	/// <summary>Above the bar's high (top of the pane outside the price chart).</summary>
	AboveBar,

	/// <summary>Below the bar's low (bottom of the pane outside the price chart).</summary>
	BelowBar,

	/// <summary>At the value of the series passed as <c>at</c>.</summary>
	AtValue,
}

public enum InputKind
{
	Int,
	Double,
	Bool,
}

public enum PlotKind
{
	Line,
	Histogram,
	Band,
	Markers,
	Level,
}
