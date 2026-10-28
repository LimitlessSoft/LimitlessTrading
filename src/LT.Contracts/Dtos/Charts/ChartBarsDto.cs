using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Charts;

public class ChartBarsDto
{
	public IReadOnlyList<BarDto> Bars { get; set; } = [];
	public DateTimeOffset FetchedAt { get; set; }

	/// <summary>True when IB could not be reached and these are the last cached bars.</summary>
	public bool IsStale { get; set; }
	public string? Error { get; set; }

	/// <summary>
	/// Where the period starts (the chart's view fits from here; older bars are loaded too); null
	/// for Auto.
	/// </summary>
	public DateTimeOffset? From { get; set; }

	/// <summary>The timeframe's longest period when the asked period was cut to it.</summary>
	public ChartPeriod? LimitedTo { get; set; }
}
