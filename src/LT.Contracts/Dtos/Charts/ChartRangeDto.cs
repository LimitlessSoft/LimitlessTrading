using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Charts;

/// <summary>What to ask IB for a chart's period, and where its bars start.</summary>
public class ChartRangeDto
{
	/// <summary>IB durationStr for reqHistoricalData (end is always now).</summary>
	public string Duration { get; set; } = "";

	/// <summary>Bars ending before this are dropped; null (Auto) keeps all IB returns.</summary>
	public DateTimeOffset? From { get; set; }

	/// <summary>
	/// The longest period of the timeframe when the asked one was longer and was cut to it; null
	/// when the period fits.
	/// </summary>
	public ChartPeriod? LimitedTo { get; set; }
}
