namespace LT.Indicators.Sdk;

/// <summary>An input an indicator declared, with its default and range.</summary>
public sealed record InputInfo(
	string Name,
	InputKind Kind,
	double DefaultValue,
	double? Min,
	double? Max,
	double? Step
);

/// <summary>One thing an indicator draws.</summary>
public sealed record PlotOutput(string Name, PlotKind Kind, Color Color, double[] Values)
{
	/// <summary>Band: its fill; histogram: the colour below 0.</summary>
	public Color? Color2 { get; init; }

	/// <summary>Band: the lower line; markers: where they sit with <see cref="MarkerPosition.AtValue"/>.</summary>
	public double[]? Values2 { get; init; }

	public double Width { get; init; } = 1.5;
	public LineStyle LineStyle { get; init; }
	public double Level { get; init; }
	public MarkerShape Shape { get; init; }
	public MarkerPosition Position { get; init; }
}

/// <summary>What one run of an indicator produced, and what it declared on the way.</summary>
public sealed record IndicatorOutput(
	string Name,
	string? ShortName,
	PaneKind Pane,
	double? ScaleMin,
	double? ScaleMax,
	int? Decimals,
	IReadOnlyList<InputInfo> Inputs,
	IReadOnlyList<PlotOutput> Plots
);
