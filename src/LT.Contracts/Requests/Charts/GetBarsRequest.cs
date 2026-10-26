using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;

namespace LT.Contracts.Requests.Charts;

public class GetBarsRequest
{
	public ContractDto Contract { get; set; } = new();
	public ChartTimeframe Timeframe { get; set; }

	/// <summary>The span of bars, ending now; Auto is the timeframe's own span.</summary>
	public ChartPeriod Period { get; set; }

	/// <summary>The zone calendar periods (today, this week…) are counted in: the display zone.</summary>
	public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;
	public bool UseRegularTradingHours { get; set; } = true;
}
