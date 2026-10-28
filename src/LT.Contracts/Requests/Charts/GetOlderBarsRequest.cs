using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;

namespace LT.Contracts.Requests.Charts;

/// <summary>A page of a chart's history before the oldest bar it holds.</summary>
public class GetOlderBarsRequest
{
	public ContractDto Contract { get; set; } = new();
	public ChartTimeframe Timeframe { get; set; }
	public bool UseRegularTradingHours { get; set; } = true;

	/// <summary>The oldest bar's time: the page ends here and holds only older bars.</summary>
	public DateTimeOffset Before { get; set; }
}
