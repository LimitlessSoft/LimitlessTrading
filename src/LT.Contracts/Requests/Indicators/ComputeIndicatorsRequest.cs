using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Indicators;

namespace LT.Contracts.Requests.Indicators;

/// <summary>A chart's indicators over its bars.</summary>
public class ComputeIndicatorsRequest
{
	public IReadOnlyList<BarDto> Bars { get; set; } = [];
	public IReadOnlyList<IndicatorInstanceDto> Indicators { get; set; } = [];
	public bool IsIntraday { get; set; }
}
