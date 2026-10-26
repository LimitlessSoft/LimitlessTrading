using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Chart;

public record TimeframeOption(ChartTimeframe Value)
{
	public override string ToString() => ChartTimeframeHelpers.GetDisplayName(Value);
}
