namespace LT.Contracts.Dtos.Charts;

/// <summary>What the chart's ruler (crosshair mode, press and drag) measures from its start to its end.</summary>
public class ChartRulerMeasureDto
{
	/// <summary>End price minus start price.</summary>
	public double Change { get; init; }

	/// <summary>The change as a percentage of the start price; null when the start price is 0.</summary>
	public double? Percent { get; init; }

	/// <summary>Bars from the start to the end; negative when the end is left of the start.</summary>
	public int Bars { get; init; }

	/// <summary>Time from the start bar to the end bar; negative when the end is earlier.</summary>
	public TimeSpan Elapsed { get; init; }

	/// <summary>The end is at or above the start (drawn green; below it, red).</summary>
	public bool IsUp => Change >= 0;
}
