using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Indicators;

/// <summary>One thing an indicator draws; values align with the bars (NaN: nothing there).</summary>
public class IndicatorPlotDto
{
	public string Name { get; set; } = string.Empty;
	public IndicatorPlotKind Kind { get; set; }

	/// <summary>0xAARRGGBB.</summary>
	public uint Color { get; set; }

	/// <summary>Band: its fill; histogram: the colour below 0 (null: <see cref="Color"/>).</summary>
	public uint? Color2 { get; set; }

	public double Width { get; set; } = 1.5;
	public IndicatorLineStyle LineStyle { get; set; }
	public double[] Values { get; set; } = [];

	/// <summary>Band: the lower line; markers: where they sit at <see cref="IndicatorMarkerPosition.AtValue"/>.</summary>
	public double[]? Values2 { get; set; }

	/// <summary>Level: its value.</summary>
	public double Level { get; set; }

	public IndicatorMarkerShape Shape { get; set; }
	public IndicatorMarkerPosition Position { get; set; }
}
