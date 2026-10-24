using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;

namespace LT.Contracts.Requests.Charts;

public class GetBarsRequest
{
	public ContractDto Contract { get; set; } = new();
	public ChartTimeframe Timeframe { get; set; }
	public bool UseRegularTradingHours { get; set; } = true;
}
