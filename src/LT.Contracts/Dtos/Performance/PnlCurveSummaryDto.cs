namespace LT.Contracts.Dtos.Performance;

/// <summary>Where an intraday P&amp;L curve is now, its extremes and its worst fall from a peak.</summary>
public class PnlCurveSummaryDto
{
	public double? Last { get; set; }
	public double? High { get; set; }
	public double? Low { get; set; }

	/// <summary>Largest drop from a running peak to a later point (0 or more).</summary>
	public double? MaxDrawdown { get; set; }
}
