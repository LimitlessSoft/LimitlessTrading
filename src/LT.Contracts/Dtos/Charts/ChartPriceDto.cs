namespace LT.Contracts.Dtos.Charts;

/// <summary>
/// A live price that moves a chart's last bar (<c>BarHelpers.MergePrice</c>): a last trade, or
/// the midpoint for forex. Carries no volume — the 5-second bars do.
/// </summary>
public class ChartPriceDto
{
	/// <summary>When it traded: IB's time for tick-by-tick (whole seconds), arrival for quotes.</summary>
	public DateTimeOffset Time { get; set; }

	public double Price { get; set; }
}
